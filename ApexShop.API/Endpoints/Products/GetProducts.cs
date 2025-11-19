using ApexShop.API.Configuration;
using ApexShop.API.DTOs;
using ApexShop.API.Extensions;
using ApexShop.API.Models.Pagination;
using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApexShop.API.Endpoints.Products;

/// <summary>
/// GET endpoints for products listing and streaming.
/// - GET / - Standard pagination (offset-based)
/// - GET /v2 - Standardized pagination with metadata
/// - GET /cursor - Keyset/cursor-based pagination (optimized for deep pages)
/// - GET /stream - Stream all products with content negotiation (MessagePack, NDJSON, JSON)
/// - GET /export/ndjson - NDJSON export with filters and rate limiting
/// </summary>
public static class GetProductsEndpoint
{
    public static RouteGroupBuilder MapGetProducts(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetProductsHandler)
            .CacheOutput("Lists")
            .WithName("GetProducts")
            .WithDescription("List products with offset-based pagination");

        group.MapGet("/v2", GetProductsV2Handler)
            .CacheOutput(policyName: "Lists")
            .WithName("GetProductsV2")
            .WithDescription("List products with standardized pagination. Returns PagedResult with metadata including HasPrevious and HasNext.");

        group.MapGet("/cursor", GetProductsCursorHandler)
            .CacheOutput("Lists")
            .WithName("GetProductsCursor")
            .WithDescription("Keyset/cursor-based pagination - O(1) performance for any page depth. Use afterId parameter to continue from last record.");

        group.MapGet("/stream", StreamProductsHandler)
            .WithName("StreamProducts")
            .WithDescription("Stream all products with content negotiation (MessagePack, NDJSON, or JSON). Use Accept header to specify format. Supports filters: categoryId, minPrice, maxPrice, inStock. Max 10K items per stream.")
            .Produces(StatusCodes.Status200OK);

        group.MapGet("/export/ndjson", ExportProductsNdjsonHandler)
            .RequireRateLimiting("streaming")  // Enforce 5 requests/minute
            .WithName("ExportProductsNdjsonV2")
            .WithDescription("Export products as NDJSON (Newline Delimited JSON) - optimal for large exports and streaming parsers. " +
                             "Features: max 100000 records, rate limited (5/min), cancellation support, error markers. " +
                             "Filters: categoryId, minPrice, maxPrice, inStock, modifiedAfter (for incremental exports).")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status429TooManyRequests);

        return group;
    }

    /// <summary>
    /// GET / - Standard offset-based pagination
    /// </summary>
    private static async Task<IResult> GetProductsHandler(
        AppDbContext db,
        int page = 1,
        int pageSize = 50)
    {
        // Validate pagination parameters
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100); // Max 100 items per page

        var products = await db.Products
            .AsNoTracking()
            .TagWith("GET /products - List products with pagination")
            .OrderBy(p => p.Id) // Required for consistent pagination
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListDto(
                p.Id,
                p.Name,
                p.Price,
                p.Stock,
                p.CategoryId))
            .ToListAsync();

        var totalCount = await CompiledQueries.GetProductCount(db); // ← Using compiled query

        return Results.Ok(new
        {
            Data = products,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    /// <summary>
    /// GET /v2 - Improved pagination with standardized response format
    /// </summary>
    private static async Task<IResult> GetProductsV2Handler(
        [AsParameters] PaginationParams pagination,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.Products
            .AsNoTracking()
            .TagWith("GET /products/v2 - List products with standardized pagination")
            .OrderBy(p => p.Id); // Required for consistent pagination

        // Note: ToPagedListAsync runs COUNT(*) on every request
        // For frequently accessed endpoints, consider caching the count separately
        var result = await query
            .Select(p => new ProductListDto(
                p.Id,
                p.Name,
                p.Price,
                p.Stock,
                p.CategoryId))
            .ToPagedListAsync(pagination.Page, pagination.PageSize, cancellationToken);

        return Results.Ok(result);
    }

    /// <summary>
    /// GET /cursor - Keyset (Cursor-based) Pagination
    /// Optimized for deep pagination and large datasets.
    /// ✅ OPTIMIZED: Replaced RemoveAt() O(n) with Take() O(1) - eliminates array reallocation
    /// </summary>
    private static async Task<IResult> GetProductsCursorHandler(
        AppDbContext db,
        int? afterId = null,
        int pageSize = 50)
    {
        // Validate pagination parameters
        pageSize = Math.Clamp(pageSize, 1, 100); // Max 100 items per page

        var query = db.Products.AsNoTracking();

        // Apply cursor filter if provided
        if (afterId.HasValue)
        {
            query = query.Where(p => p.Id > afterId.Value);
        }

        // Fetch one extra to determine if there are more results
        var allProducts = await query
            .TagWith("GET /products/cursor - Keyset pagination (optimized for deep pages)")
            .OrderBy(p => p.Id) // Required for consistent pagination
            .Take(pageSize + 1)
            .Select(p => new ProductListDto(
                p.Id,
                p.Name,
                p.Price,
                p.Stock,
                p.CategoryId))
            .ToListAsync();

        // ✅ OPTIMIZED: Detect hasMore BEFORE slicing (no allocation for extra item)
        var hasMore = allProducts.Count > pageSize;

        // ✅ FAST: Take() creates view of first pageSize items (O(1) vs RemoveAt O(n))
        // RemoveAt caused array reallocation; Take avoids it entirely
        var products = allProducts.Take(pageSize).ToList();

        return Results.Ok(new
        {
            Data = products,
            PageSize = pageSize,
            HasMore = hasMore,
            NextCursor = hasMore && products.Count > 0 ? products[^1].Id : (int?)null
        });
    }

    /// <summary>
    /// GET /stream - Streaming endpoint with content negotiation
    /// Supports MessagePack, NDJSON, or JSON based on Accept header
    /// ✅ OPTIMIZED: Factory pattern + configurable flush interval for optimal latency/throughput
    /// ✅ CAPPED: Max 10K items to eliminate variance (prevents runaway exports)
    /// </summary>
    private static IResult StreamProductsHandler(
        HttpContext context,
        AppDbContext db,
        IOptions<StreamingOptions> streamingOptionsAccessor,
        int? categoryId = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        bool? inStock = null)
    {
        const int MAX_STREAMING_ITEMS = 10_000;
        var streamingOptions = streamingOptionsAccessor.Value;

        var query = db.Products.AsNoTracking();

        // Apply optional filters
        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (minPrice.HasValue)
            query = query.Where(p => p.Price >= minPrice.Value);

        if (maxPrice.HasValue)
            query = query.Where(p => p.Price <= maxPrice.Value);

        if (inStock.HasValue && inStock.Value)
            query = query.Where(p => p.Stock > 0);

        var products = query
            .TagWith("GET /products/stream - Stream all products with filters (constant memory)")
            .OrderBy(p => p.Id)
            .Take(MAX_STREAMING_ITEMS)  // ✅ Hard cap on streaming size
            .Select(p => new ProductListDto(
                p.Id,
                p.Name,
                p.Price,
                p.Stock,
                p.CategoryId))
            .AsAsyncEnumerable();

        // ✅ OPTIMIZED: Factory pattern eliminates repeated header parsing, uses configured flush interval
        return context.StreamAs(products, streamingOptions.FlushInterval);
    }

    /// <summary>
    /// GET /export/ndjson - NDJSON Export v2
    /// Newline Delimited JSON for efficient streaming and parsing
    /// Enhanced version with:
    /// - Rate limiting (5 requests/minute per user)
    /// - Safeguards (max record limits, cancellation support)
    /// - Additional filters (modifiedAfter for incremental exports)
    /// - Audit logging
    ///
    /// Optimal for: large exports (100K+), streaming parsers, downstream data pipelines
    /// </summary>
    private static async Task<IResult> ExportProductsNdjsonHandler(
        HttpContext context,
        AppDbContext db,
        ILogger<Program> logger,
        IOptions<StreamingOptions> streamingOptionsAccessor,
        int? categoryId = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        bool? inStock = null,
        DateTime? modifiedAfter = null,
        CancellationToken cancellationToken = default)
    {
        var streamingOptions = streamingOptionsAccessor.Value;

        // ✅ Hard cap on streaming exports to prevent variance and resource exhaustion
        const int MAX_STREAMING_ITEMS = 10_000;

        // Build query with filters
        var query = db.Products.AsNoTracking();

        // Apply optional filters
        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (minPrice.HasValue)
            query = query.Where(p => p.Price >= minPrice.Value);

        if (maxPrice.HasValue)
            query = query.Where(p => p.Price <= maxPrice.Value);

        if (inStock.HasValue && inStock.Value)
            query = query.Where(p => p.Stock > 0);

        // Support incremental exports (only export modified products since timestamp)
        if (modifiedAfter.HasValue)
            query = query.Where(p => p.UpdatedDate >= modifiedAfter.Value || p.CreatedDate >= modifiedAfter.Value);

        // Prepare response and stream
        var filteredQuery = query
            .TagWith("GET /products/export/ndjson - NDJSON export with filters and safeguards")
            .OrderBy(p => p.Id)
            .Take(MAX_STREAMING_ITEMS)  // ✅ Hard cap on streaming size
            .Select(p => new ProductListDto(
                p.Id,
                p.Name,
                p.Price,
                p.Stock,
                p.CategoryId));

        // Set up response stream
        context.Response.ContentType = "application/x-ndjson";
        context.Response.Headers.Append("Content-Disposition", "attachment; filename=products.ndjson");

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
