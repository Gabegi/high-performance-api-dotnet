using ApexShop.Application.DTOs;
using ApexShop.API.Extensions;
using ApexShop.API.JsonContext;
using ApexShop.API.Models.Pagination;
using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace ApexShop.API.Endpoints.Reviews;

/// <summary>
/// GET endpoints for reviews listing and streaming.
/// - GET / - Standard pagination (offset-based)
/// - GET /v2 - Standardized pagination with metadata
/// - GET /cursor - Keyset/cursor-based pagination (optimized for deep pages)
/// - GET /stream - Stream all reviews with content negotiation
/// - GET /export/ndjson - NDJSON export
/// </summary>
public static class GetReviewsEndpoint
{
    public static RouteGroupBuilder MapGetReviews(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetReviewsHandler)
            .CacheOutput("Lists")
            .WithName("GetReviews")
            .WithDescription("List reviews with offset-based pagination");

        group.MapGet("/v2", GetReviewsV2Handler)
            .CacheOutput(policyName: "Lists")
            .WithName("GetReviewsV2")
            .WithDescription("List reviews with standardized pagination. Returns PagedResult with metadata including HasPrevious and HasNext.");

        group.MapGet("/cursor", GetReviewsCursorHandler)
            .CacheOutput("Lists")
            .WithName("GetReviewsCursor")
            .WithDescription("Keyset/cursor-based pagination - O(1) performance for any page depth. Use afterId parameter to continue from last record.");

        group.MapGet("/stream", StreamReviewsHandler)
            .WithName("StreamReviews")
            .WithDescription("Stream all reviews with content negotiation (MessagePack, NDJSON, or JSON). Use Accept header to specify format. Supports filters: productId, userId, minRating")
            .Produces(StatusCodes.Status200OK);

        group.MapGet("/export/ndjson", ExportReviewsNdjsonHandler)
            .WithName("ExportReviewsNdjson")
            .WithDescription("Export reviews as NDJSON. Supports filters: productId, userId, minRating. Max 50K items.")
            .Produces(StatusCodes.Status200OK);

        return group;
    }

    /// <summary>
    /// GET / - Standard offset-based pagination
    /// </summary>
    private static async Task<IResult> GetReviewsHandler(
        AppDbContext db,
        int page = 1,
        int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var reviews = await db.Reviews
            .AsNoTracking()
            .TagWith("GET /reviews - List reviews with pagination")
            .OrderByDescending(r => r.CreatedDate) // Most recent reviews first
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReviewListDto(
                r.Id,
                r.ProductId,
                r.UserId,
                r.Rating,
                r.IsVerifiedPurchase))
            .ToListAsync();

        var totalCount = await CompiledQueries.GetReviewCount(db); // ← Using compiled query

        return Results.Ok(new
        {
            Data = reviews,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    /// <summary>
    /// GET /v2 - Improved pagination with standardized response format
    /// </summary>
    private static async Task<IResult> GetReviewsV2Handler(
        [AsParameters] PaginationParams pagination,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.Reviews
            .AsNoTracking()
            .TagWith("GET /reviews/v2 - List reviews with standardized pagination")
            .OrderByDescending(r => r.CreatedDate); // Most recent reviews first - stable sort

        // Note: ToPagedListAsync runs COUNT(*) on every request
        // For frequently accessed endpoints, consider caching the count separately
        var result = await query
            .Select(r => new ReviewListDto(
                r.Id,
                r.ProductId,
                r.UserId,
                r.Rating,
                r.IsVerifiedPurchase))
            .ToPagedListAsync(pagination.Page, pagination.PageSize, cancellationToken);

        return Results.Ok(result);
    }

    /// <summary>
    /// GET /cursor - Keyset (Cursor-based) Pagination
    /// Optimized for deep pagination and large datasets.
    /// </summary>
    private static async Task<IResult> GetReviewsCursorHandler(
        AppDbContext db,
        int? afterId = null,
        int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 100); // Max 100 items per page

        var query = db.Reviews.AsNoTracking();

        if (afterId.HasValue)
        {
            query = query.Where(r => r.Id > afterId.Value);
        }

        var allReviews = await query
            .TagWith("GET /reviews/cursor - Keyset pagination (optimized for deep pages)")
            .OrderBy(r => r.Id)
            .Take(pageSize + 1)
            .Select(r => new ReviewListDto(
                r.Id,
                r.ProductId,
                r.UserId,
                r.Rating,
                r.IsVerifiedPurchase))
            .ToListAsync();

        var hasMore = allReviews.Count > pageSize;
        var reviews = allReviews.Take(pageSize).ToList();

        return Results.Ok(new
        {
            Data = reviews,
            PageSize = pageSize,
            HasMore = hasMore,
            NextCursor = hasMore && reviews.Count > 0 ? reviews[^1].Id : (int?)null
        });
    }

    /// <summary>
    /// GET /stream - Streaming endpoint with content negotiation
    /// </summary>
    private static IResult StreamReviewsHandler(
        HttpContext context,
        AppDbContext db,
        int? productId = null,
        int? userId = null,
        int? minRating = null)
    {
        var query = db.Reviews.AsNoTracking();

        if (productId.HasValue)
            query = query.Where(r => r.ProductId == productId.Value);

        if (userId.HasValue)
            query = query.Where(r => r.UserId == userId.Value);

        if (minRating.HasValue)
            query = query.Where(r => r.Rating >= minRating.Value);

        var reviews = query
            .TagWith("GET /reviews/stream - Stream all reviews with filters (constant memory)")
            .OrderBy(r => r.Id)
            .Select(r => new ReviewListDto(
                r.Id,
                r.ProductId,
                r.UserId,
                r.Rating,
                r.IsVerifiedPurchase))
            .AsAsyncEnumerable();

        return context.StreamAs(reviews);
    }

    /// <summary>
    /// GET /export/ndjson - NDJSON Export
    /// </summary>
    private static async Task<IResult> ExportReviewsNdjsonHandler(
        HttpContext context,
        AppDbContext db,
        int? productId = null,
        int? userId = null,
        int? minRating = null,
        int limit = 50000,
        CancellationToken cancellationToken = default)
    {
        try
        {
            limit = Math.Clamp(limit, 1, 50000);
            context.Response.ContentType = "application/x-ndjson";
            context.Response.Headers.Append("Content-Disposition", "attachment; filename=reviews.ndjson");

            var query = db.Reviews.AsNoTracking();
            if (productId.HasValue)
                query = query.Where(r => r.ProductId == productId.Value);
            if (userId.HasValue)
                query = query.Where(r => r.UserId == userId.Value);
            if (minRating.HasValue)
                query = query.Where(r => r.Rating >= minRating.Value);

            var filteredQuery = query
                .TagWith("GET /reviews/export/ndjson - NDJSON export")
                .OrderBy(r => r.Id)
                .Take(limit)
                .Select(r => new ReviewListDto(r.Id, r.ProductId, r.UserId, r.Rating, r.IsVerifiedPurchase));

            int exportedCount = 0;
            await foreach (var review in filteredQuery.AsAsyncEnumerable().WithCancellation(cancellationToken))
            {
                await JsonSerializer.SerializeAsync(context.Response.Body, review, ApexShopJsonContext.Default.ReviewListDto, cancellationToken);
                await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("\n"), cancellationToken);
                if (++exportedCount % 100 == 0)
                    await context.Response.Body.FlushAsync(cancellationToken);
            }
            await context.Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            context.Response.HttpContext.Abort();
        }
        catch
        {
            context.Response.HttpContext.Abort();
            throw;
        }

        return Results.Empty;
    }
}
