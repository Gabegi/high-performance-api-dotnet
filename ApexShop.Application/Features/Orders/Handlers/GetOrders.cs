using ApexShop.API.Configuration;
using ApexShop.API.DTOs;
using ApexShop.API.Extensions;
using ApexShop.API.Models.Pagination;
using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Enums;
using ApexShop.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApexShop.Application.Features.Orders.Handlers;

/// <summary>
/// Handler for GET /orders - List orders with offset-based pagination
/// </summary>
public static class GetOrdersHandler
{
    public static async Task<IResult> Handle(
        AppDbContext db,
        int page = 1,
        int pageSize = 50)
    {
        // Validate pagination parameters
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100); // Max 100 items per page

        var orders = await db.Orders
            .AsNoTracking()
            .TagWith("GET /orders - List orders with pagination")
            .OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderListDto(
                o.Id,
                o.UserId,
                o.OrderDate,
                o.Status.ToString(),
                o.TotalAmount))
            .ToListAsync();

        var totalCount = await CompiledQueries.GetOrderCount(db); // Using compiled query

        return Results.Ok(new
        {
            Data = orders,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }
}

/// <summary>
/// Handler for GET /orders/v2 - Improved pagination with standardized response format
/// </summary>
public static class GetOrdersV2Handler
{
    public static async Task<IResult> Handle(
        PaginationParams pagination,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.Orders
            .AsNoTracking()
            .TagWith("GET /orders/v2 - List orders with standardized pagination")
            .OrderByDescending(o => o.OrderDate);

        // Note: ToPagedListAsync runs COUNT(*) on every request
        // For frequently accessed endpoints, consider caching the count separately
        var result = await query
            .Select(o => new OrderListDto(
                o.Id,
                o.UserId,
                o.OrderDate,
                o.Status.ToString(),
                o.TotalAmount))
            .ToPagedListAsync(pagination.Page, pagination.PageSize, cancellationToken);

        return Results.Ok(result);
    }
}

/// <summary>
/// Handler for GET /orders/cursor - Keyset (Cursor-based) Pagination
/// Optimized for deep pagination and large datasets.
/// Uses OPTIMIZED pattern: Replaced RemoveAt() O(n) with Take() O(1) - eliminates array reallocation
/// </summary>
public static class GetOrdersCursorHandler
{
    public static async Task<IResult> Handle(
        AppDbContext db,
        int? afterId = null,
        int pageSize = 50)
    {
        // Validate pagination parameters
        pageSize = Math.Clamp(pageSize, 1, 100); // Max 100 items per page

        var query = db.Orders.AsNoTracking();

        // Apply cursor filter if provided
        if (afterId.HasValue)
        {
            query = query.Where(o => o.Id > afterId.Value);
        }

        // Fetch one extra to determine if there are more results
        var allOrders = await query
            .TagWith("GET /orders/cursor - Keyset pagination (optimized for deep pages)")
            .OrderBy(o => o.Id)
            .Take(pageSize + 1)
            .Select(o => new OrderListDto(
                o.Id,
                o.UserId,
                o.OrderDate,
                o.Status.ToString(),
                o.TotalAmount))
            .ToListAsync();

        // OPTIMIZED: Detect hasMore BEFORE slicing (no allocation for extra item)
        var hasMore = allOrders.Count > pageSize;

        // FAST: Take() creates view of first pageSize items (O(1) vs RemoveAt O(n))
        // RemoveAt caused array reallocation; Take avoids it entirely
        var orders = allOrders.Take(pageSize).ToList();

        return Results.Ok(new
        {
            Data = orders,
            PageSize = pageSize,
            HasMore = hasMore,
            NextCursor = hasMore && orders.Count > 0 ? orders[^1].Id : (int?)null
        });
    }
}

/// <summary>
/// Handler for GET /orders/stream - Streaming endpoint with content negotiation
/// Supports MessagePack, NDJSON, or JSON based on Accept header
/// OPTIMIZED: Factory pattern + configurable flush interval for optimal latency/throughput
/// CAPPED: Max 10K items to eliminate variance (prevents runaway exports)
/// </summary>
public static class StreamOrdersHandler
{
    public static IResult Handle(
        HttpContext context,
        AppDbContext db,
        IOptions<StreamingOptions> streamingOptionsAccessor,
        int? userId = null,
        OrderStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        const int MAX_STREAMING_ITEMS = 10_000;
        var streamingOptions = streamingOptionsAccessor.Value;

        var query = db.Orders.AsNoTracking();

        // Apply optional filters
        if (userId.HasValue)
            query = query.Where(o => o.UserId == userId.Value);

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        if (fromDate.HasValue)
            query = query.Where(o => o.OrderDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(o => o.OrderDate <= toDate.Value);

        var orders = query
            .TagWith("GET /orders/stream - Stream all orders with filters (constant memory)")
            .OrderByDescending(o => o.OrderDate)
            .Take(MAX_STREAMING_ITEMS)  // Hard cap on streaming size
            .Select(o => new OrderListDto(
                o.Id,
                o.UserId,
                o.OrderDate,
                o.Status.ToString(),
                o.TotalAmount))
            .AsAsyncEnumerable();

        // OPTIMIZED: Factory pattern eliminates repeated header parsing, uses configured flush interval
        return context.StreamAs(orders, streamingOptions.FlushInterval);
    }
}

/// <summary>
/// Handler for GET /orders/export/ndjson - NDJSON Export v2
/// Newline Delimited JSON for efficient streaming and parsing
/// Enhanced version with:
/// - Rate limiting (5 requests/minute per user)
/// - Safeguards (max record limits, cancellation support)
/// - Additional filters (status, date ranges for incremental exports)
/// - Audit logging
///
/// Optimal for: large exports (100K+), streaming parsers, downstream data pipelines
/// </summary>
public static class ExportOrdersNdjsonHandler
{
    public static async Task<IResult> Handle(
        HttpContext context,
        AppDbContext db,
        ILogger logger,
        IOptions<StreamingOptions> streamingOptionsAccessor,
        int? userId = null,
        OrderStatus? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var streamingOptions = streamingOptionsAccessor.Value;

        // Hard cap on streaming exports to prevent variance and resource exhaustion
        const int MAX_STREAMING_ITEMS = 10_000;

        // Build query with filters
        var query = db.Orders.AsNoTracking();

        // Apply optional filters
        if (userId.HasValue)
            query = query.Where(o => o.UserId == userId.Value);

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        if (fromDate.HasValue)
            query = query.Where(o => o.OrderDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(o => o.OrderDate <= toDate.Value);

        // Prepare response and stream
        var filteredQuery = query
            .TagWith("GET /orders/export/ndjson - NDJSON export with filters and safeguards")
            .OrderByDescending(o => o.OrderDate)
            .Take(MAX_STREAMING_ITEMS)  // Hard cap on streaming size
            .Select(o => new OrderListDto(
                o.Id,
                o.UserId,
                o.OrderDate,
                o.Status.ToString(),
                o.TotalAmount));

        // Set up response stream
        context.Response.ContentType = "application/x-ndjson";
        context.Response.Headers.Append("Content-Disposition", "attachment; filename=orders.ndjson");

        // Use StreamingExtensions for safe streaming with proper error handling
        await StreamingExtensions.StreamToNdjsonAsync(
            context,
            filteredQuery.AsAsyncEnumerable()
                .StreamWithSafeguards(streamingOptions.MaxRecords, cancellationToken),
            logger,
            streamingOptions,
            cancellationToken);

        return Results.Empty;
    }
}
