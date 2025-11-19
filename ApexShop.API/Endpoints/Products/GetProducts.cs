using ApexShop.Application.Features.Products.Handlers;
using ApexShop.API.Models.Pagination;
using ApexShop.API.Configuration;
using Microsoft.AspNetCore.OutputCaching;
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
        group.MapGet("/", GetProductsHandlerWrapper)
            .CacheOutput("Lists")
            .WithName("GetProducts")
            .WithDescription("List products with offset-based pagination");

        group.MapGet("/v2", GetProductsV2HandlerWrapper)
            .CacheOutput(policyName: "Lists")
            .WithName("GetProductsV2")
            .WithDescription("List products with standardized pagination. Returns PagedResult with metadata including HasPrevious and HasNext.");

        group.MapGet("/cursor", GetProductsCursorHandlerWrapper)
            .CacheOutput("Lists")
            .WithName("GetProductsCursor")
            .WithDescription("Keyset/cursor-based pagination - O(1) performance for any page depth. Use afterId parameter to continue from last record.");

        group.MapGet("/stream", StreamProductsHandlerWrapper)
            .WithName("StreamProducts")
            .WithDescription("Stream all products with content negotiation (MessagePack, NDJSON, or JSON). Use Accept header to specify format. Supports filters: categoryId, minPrice, maxPrice, inStock. Max 10K items per stream.")
            .Produces(StatusCodes.Status200OK);

        group.MapGet("/export/ndjson", ExportProductsNdjsonHandlerWrapper)
            .RequireRateLimiting("streaming")  // Enforce 5 requests/minute
            .WithName("ExportProductsNdjsonV2")
            .WithDescription("Export products as NDJSON (Newline Delimited JSON) - optimal for large exports and streaming parsers. " +
                             "Features: max 100000 records, rate limited (5/min), cancellation support, error markers. " +
                             "Filters: categoryId, minPrice, maxPrice, inStock, modifiedAfter (for incremental exports).")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status429TooManyRequests);

        return group;
    }

    private static async Task<IResult> GetProductsHandlerWrapper(
        AppDbContext db,
        int page = 1,
        int pageSize = 50) =>
        await GetProductsHandler.Handle(db, page, pageSize);

    private static async Task<IResult> GetProductsV2HandlerWrapper(
        PaginationParams pagination,
        AppDbContext db,
        CancellationToken cancellationToken) =>
        await GetProductsV2Handler.Handle(pagination, db, cancellationToken);

    private static async Task<IResult> GetProductsCursorHandlerWrapper(
        AppDbContext db,
        int? afterId = null,
        int pageSize = 50) =>
        await GetProductsCursorHandler.Handle(db, afterId, pageSize);

    private static IResult StreamProductsHandlerWrapper(
        HttpContext context,
        AppDbContext db,
        IOptions<StreamingOptions> streamingOptionsAccessor,
        int? categoryId = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        bool? inStock = null) =>
        StreamProductsHandler.Handle(context, db, streamingOptionsAccessor, categoryId, minPrice, maxPrice, inStock);

    private static async Task<IResult> ExportProductsNdjsonHandlerWrapper(
        HttpContext context,
        AppDbContext db,
        ILogger<Program> logger,
        IOptions<StreamingOptions> streamingOptionsAccessor,
        int? categoryId = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        bool? inStock = null,
        DateTime? modifiedAfter = null,
        CancellationToken cancellationToken = default) =>
        await ExportProductsNdjsonHandler.Handle(context, db, logger, streamingOptionsAccessor, categoryId, minPrice, maxPrice, inStock, modifiedAfter, cancellationToken);
}
