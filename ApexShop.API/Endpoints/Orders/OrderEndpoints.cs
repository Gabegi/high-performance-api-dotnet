using ApexShop.API.Configuration;
using ApexShop.API.DTOs;
using ApexShop.API.Extensions;
using ApexShop.API.JsonContext;
using ApexShop.API.Models.Pagination;
using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Enums;
using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace ApexShop.API.Endpoints.Orders;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders").WithTags("Orders");

        group.MapGet("/", async (AppDbContext db, int page = 1, int pageSize = 50) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var orders = await db.Orders
                .AsNoTracking()
                .TagWith("GET /orders - List orders with pagination")
                .OrderByDescending(o => o.OrderDate) // Most recent orders first
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OrderListDto(
                    o.Id,
                    o.UserId,
                    o.OrderDate,
                    o.Status.ToString(),
                    o.TotalAmount))
                .ToListAsync();

            var totalCount = await CompiledQueries.GetOrderCount(db); // ← Using compiled query

            return Results.Ok(new
            {
                Data = orders,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        })
        .CacheOutput("Lists");

        // V2: Improved pagination with standardized response format
        group.MapGet("/v2", async ([AsParameters] PaginationParams pagination, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var query = db.Orders
                .AsNoTracking()
                .TagWith("GET /orders/v2 - List orders with standardized pagination")
                .OrderByDescending(o => o.OrderDate); // Most recent orders first - stable sort

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
        })
        .CacheOutput(policyName: "Lists")
        .WithName("GetOrdersV2")
        .WithDescription("List orders with standardized pagination. Returns PagedResult with metadata including HasPrevious and HasNext.");

        // Keyset (Cursor-based) Pagination - Optimized for deep pagination and large datasets
        group.MapGet("/cursor", async (AppDbContext db, int? afterId = null, int pageSize = 50) =>
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
                .OrderBy(o => o.Id) // Required for consistent pagination and optimal index usage
                .Take(pageSize + 1)
                .Select(o => new OrderListDto(
                    o.Id,
                    o.UserId,
                    o.OrderDate,
                    o.Status.ToString(),
                    o.TotalAmount))
                .ToListAsync();

            // ✅ OPTIMIZED: Detect hasMore BEFORE slicing (no allocation for extra item)
            var hasMore = allOrders.Count > pageSize;

            // ✅ FAST: Take() creates view of first pageSize items (O(1) vs RemoveAt O(n))
            var orders = allOrders.Take(pageSize).ToList();

            return Results.Ok(new
            {
                Data = orders,
                PageSize = pageSize,
                HasMore = hasMore,
                NextCursor = hasMore && orders.Count > 0 ? orders[^1].Id : (int?)null
            });
        })
        .CacheOutput("Lists")
        .WithName("GetOrdersCursor")
          .WithDescription("Keyset/cursor-based pagination - O(1) performance for any page depth. Use afterId parameter to continue from last record.");

        // Streaming - Get all orders with optional filters using IAsyncEnumerable
        // Supports content negotiation: MessagePack, NDJSON, or JSON based on Accept header
        group.MapGet("/stream", (HttpContext context, AppDbContext db, int? userId = null, string? status = null, DateTime? fromDate = null, DateTime? toDate = null) =>
        {
            var query = db.Orders.AsNoTracking();

            // Apply optional filters
            if (userId.HasValue)
                query = query.Where(o => o.UserId == userId.Value);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
                query = query.Where(o => o.Status == orderStatus);

            if (fromDate.HasValue)
                query = query.Where(o => o.OrderDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(o => o.OrderDate <= toDate.Value);

            var orders = query
                .TagWith("GET /orders/stream - Stream all orders with filters (constant memory)")
                .OrderBy(o => o.Id)
                .Select(o => new OrderListDto(
                    o.Id,
                    o.UserId,
                    o.OrderDate,
                    o.Status.ToString(),
                    o.TotalAmount))
                .AsAsyncEnumerable();

            // Content negotiation: return in client-preferred format (MessagePack, NDJSON, or JSON)
            return context.StreamAs(orders);
        }).WithName("StreamOrders")
          .WithDescription("Stream all orders with content negotiation (MessagePack, NDJSON, or JSON). Use Accept header to specify format. Supports filters: userId, status, fromDate, toDate")
          .Produces(StatusCodes.Status200OK);

        // NDJSON Export v2 - Newline Delimited JSON for efficient streaming and parsing
        // Enhanced version with:
        // - Rate limiting (5 requests/minute per user)
        // - Safeguards (max record limits, cancellation support)
        // - Additional filters (minAmount, maxAmount for value-based exports)
        // - Audit logging
        //
        // Optimal for: large order exports (100K+), analytics pipelines, data warehousing
        group.MapGet("/export/ndjson", async (
            HttpContext context,
            AppDbContext db,
            ILogger<Program> logger,
            IOptions<StreamingOptions> streamingOptionsAccessor,
            int? customerId = null,
            string? status = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            decimal? minAmount = null,
            decimal? maxAmount = null,
            CancellationToken cancellationToken = default) =>
        {
            var streamingOptions = streamingOptionsAccessor.Value;

            // Build query with filters
            var query = db.Orders.AsNoTracking();

            // Apply optional filters
            if (customerId.HasValue)
                query = query.Where(o => o.UserId == customerId.Value);

            // Support filtering by order status (Pending, Processing, Shipped, Delivered, Cancelled)
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
                query = query.Where(o => o.Status == orderStatus);

            // Support date range filtering (typical for reporting queries)
            if (fromDate.HasValue)
                query = query.Where(o => o.OrderDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(o => o.OrderDate <= toDate.Value);

            // Support amount range filtering (for high-value order exports)
            if (minAmount.HasValue)
                query = query.Where(o => o.TotalAmount >= minAmount.Value);

            if (maxAmount.HasValue)
                query = query.Where(o => o.TotalAmount <= maxAmount.Value);

            // Prepare response and stream
            var filteredQuery = query
                .TagWith("GET /orders/export/ndjson - NDJSON export with filters and safeguards")
                .OrderBy(o => o.Id)
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
        })
        .RequireRateLimiting("streaming")  // Enforce 5 requests/minute
        .WithName("ExportOrdersNdjsonV2")
        .WithDescription("Export orders as NDJSON (Newline Delimited JSON) - optimal for large exports and analytics pipelines. " +
                         "Features: max 100000 records, rate limited (5/min), cancellation support, error markers. " +
                         "Filters: customerId, status (Pending/Processing/Shipped/Delivered/Cancelled), fromDate, toDate, minAmount, maxAmount.")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
        {
            var order = await CompiledQueries.GetOrderById(db, id);
            if (order is null) return Results.NotFound();

            return Results.Ok(new OrderDto(
                order.Id,
                order.UserId,
                order.OrderDate,
                order.Status.ToString(),
                order.TotalAmount,
                order.ShippingAddress,
                order.TrackingNumber,
                order.ShippedDate,
                order.DeliveredDate));
        })
        .CacheOutput("Single");

        group.MapPost("/", async (Order order, AppDbContext db, IOutputCacheStore cache) =>
        {
            order.OrderDate = DateTime.UtcNow;
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            // Invalidate caches after creating new order
            await cache.EvictByTagAsync("lists", default);

            return Results.Created($"/orders/{order.Id}", order);
        });

        group.MapPut("/{id}", async (int id, Order inputOrder, AppDbContext db, IOutputCacheStore cache) =>
        {
            var order = await db.Orders.FindAsync(id);
            if (order is null) return Results.NotFound();

            order.Status = inputOrder.Status;
            order.ShippingAddress = inputOrder.ShippingAddress;
            order.TrackingNumber = inputOrder.TrackingNumber;
            order.ShippedDate = inputOrder.ShippedDate;
            order.DeliveredDate = inputOrder.DeliveredDate;
            order.TotalAmount = inputOrder.TotalAmount;

            await db.SaveChangesAsync();

            // Invalidate caches after update
            await cache.EvictByTagAsync("lists", default);
            await cache.EvictByTagAsync("single", default);

            return Results.NoContent();
        });

        group.MapDelete("/{id}", async (int id, AppDbContext db, IOutputCacheStore cache) =>
        {
            if (await db.Orders.FindAsync(id) is Order order)
            {
                db.Orders.Remove(order);
                await db.SaveChangesAsync();

                // Invalidate caches after delete
                await cache.EvictByTagAsync("lists", default);
                await cache.EvictByTagAsync("single", default);

                return Results.NoContent();
            }
            return Results.NotFound();
        });

        // ExecuteDelete - Bulk delete old orders
        group.MapDelete("/bulk-delete-old", async (int olderThanDays, AppDbContext db) =>
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
            var affectedRows = await db.Orders
                .Where(o => o.OrderDate < cutoffDate && o.Status == OrderStatus.Delivered)
                .ExecuteDeleteAsync();

            return Results.Ok(new { AffectedRows = affectedRows, Message = $"Deleted {affectedRows} orders older than {olderThanDays} days" });
        }).WithName("BulkDeleteOldOrders")
          .WithDescription("Bulk delete delivered orders older than specified days without loading entities into memory");
    }
}
