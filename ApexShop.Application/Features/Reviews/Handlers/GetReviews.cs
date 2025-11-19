using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using ApexShop.Application.DTOs;
using ApexShop.API.Models.Pagination;
using ApexShop.API.Extensions;
using ApexShop.API.JsonContext;
using ApexShop.API.Configuration;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace ApexShop.Application.Features.Reviews.Handlers;

/// <summary>
/// Handlers for retrieving reviews with various pagination and export strategies.
/// </summary>
public static class GetReviews
{
    /// <summary>
    /// GET / - Standard offset-based pagination
    /// Returns reviews with basic pagination metadata.
    /// </summary>
    public static async Task<IResult> GetReviewsHandler(
        AppDbContext db,
        int page = 1,
        int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var reviews = await db.Reviews
            .AsNoTracking()
            .TagWith("GET /reviews - List reviews with pagination")
            .OrderBy(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReviewListDto(
                r.Id,
                r.ProductId,
                r.UserId,
                r.Rating,
                r.IsVerifiedPurchase))
            .ToListAsync();

        var totalCount = await CompiledQueries.GetReviewCount(db);

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
    /// Returns PagedResult with metadata including HasPrevious and HasNext.
    /// </summary>
    public static async Task<IResult> GetReviewsV2Handler(
        PaginationParams pagination,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.Reviews
            .AsNoTracking()
            .TagWith("GET /reviews/v2 - List reviews with standardized pagination")
            .OrderBy(r => r.Id);

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
    /// More efficient than offset-based pagination for large datasets.
    /// </summary>
    public static async Task<IResult> GetReviewsCursorHandler(
        AppDbContext db,
        int? afterId = null,
        int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Reviews
            .AsNoTracking()
            .TagWith("GET /reviews/cursor - Keyset pagination")
            .OrderBy(r => r.Id);

        if (afterId.HasValue)
        {
            query = query.Where(r => r.Id > afterId.Value).OrderBy(r => r.Id);
        }

        var reviews = await query
            .Take(pageSize + 1)
            .Select(r => new ReviewListDto(
                r.Id,
                r.ProductId,
                r.UserId,
                r.Rating,
                r.IsVerifiedPurchase))
            .ToListAsync();

        var hasMore = reviews.Count > pageSize;
        if (hasMore)
        {
            reviews = reviews.Take(pageSize).ToList();
        }

        var nextCursor = hasMore ? reviews.Last().Id : (int?)null;

        return Results.Ok(new
        {
            Data = reviews,
            NextCursor = nextCursor,
            HasMore = hasMore,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// GET /stream - Streaming endpoint with content negotiation
    /// Supports MessagePack, NDJSON, or JSON based on Accept header.
    /// </summary>
    public static IResult StreamReviewsHandler(
        HttpContext context,
        AppDbContext db,
        IOptions<StreamingOptions> streamingOptionsAccessor,
        int? productId = null,
        int? userId = null,
        int? minRating = null)
    {
        const int MAX_STREAMING_ITEMS = 10_000;
        var streamingOptions = streamingOptionsAccessor.Value;

        var query = db.Reviews.AsNoTracking();

        // Apply optional filters
        if (productId.HasValue)
            query = query.Where(r => r.ProductId == productId.Value);

        if (userId.HasValue)
            query = query.Where(r => r.UserId == userId.Value);

        if (minRating.HasValue)
            query = query.Where(r => r.Rating >= minRating.Value);

        var reviews = query
            .TagWith("GET /reviews/stream - Stream all reviews with filters (constant memory)")
            .OrderBy(r => r.Id)
            .Take(MAX_STREAMING_ITEMS)
            .Select(r => new ReviewListDto(
                r.Id,
                r.ProductId,
                r.UserId,
                r.Rating,
                r.IsVerifiedPurchase))
            .AsAsyncEnumerable();

        return context.StreamAs(reviews, streamingOptions.FlushInterval);
    }

    /// <summary>
    /// GET /export/ndjson - NDJSON Export
    /// Exports reviews in newline-delimited JSON format for efficient large exports.
    /// </summary>
    public static async Task<IResult> ExportReviewsNdjsonHandler(
        HttpContext context,
        AppDbContext db,
        ILogger logger,
        IOptions<StreamingOptions> streamingOptionsAccessor,
        int? productId = null,
        int? userId = null,
        int? minRating = null,
        int limit = 10000,
        CancellationToken cancellationToken = default)
    {
        var streamingOptions = streamingOptionsAccessor.Value;
        const int MAX_STREAMING_ITEMS = 10_000;

        try
        {
            limit = Math.Clamp(limit, 1, MAX_STREAMING_ITEMS);

            // Build query with filters
            var query = db.Reviews.AsNoTracking();

            // Apply optional filters
            if (productId.HasValue)
                query = query.Where(r => r.ProductId == productId.Value);

            if (userId.HasValue)
                query = query.Where(r => r.UserId == userId.Value);

            if (minRating.HasValue)
                query = query.Where(r => r.Rating >= minRating.Value);

            // Prepare response and stream
            var filteredQuery = query
                .TagWith("GET /reviews/export/ndjson - NDJSON export with filters and safeguards")
                .OrderBy(r => r.Id)
                .Take(limit)
                .Select(r => new ReviewListDto(
                    r.Id,
                    r.ProductId,
                    r.UserId,
                    r.Rating,
                    r.IsVerifiedPurchase));

            // Set up response stream
            context.Response.ContentType = "application/x-ndjson";
            context.Response.Headers.Append("Content-Disposition", "attachment; filename=reviews.ndjson");

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
        catch (OperationCanceledException)
        {
            context.Response.HttpContext.Abort();
            return Results.Empty;
        }
        catch
        {
            context.Response.HttpContext.Abort();
            throw;
        }
    }
}
