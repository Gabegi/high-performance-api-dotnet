using ApexShop.API.DTOs;
using ApexShop.API.Extensions;
using ApexShop.API.JsonContext;
using ApexShop.API.Models.Pagination;
using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace ApexShop.API.Endpoints.Reviews;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reviews").WithTags("Reviews");

        group.MapGet("/", async (AppDbContext db, int page = 1, int pageSize = 50) =>
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
        })
        .CacheOutput("Lists");

        // V2: Improved pagination with standardized response format
        group.MapGet("/v2", async ([AsParameters] PaginationParams pagination, AppDbContext db, CancellationToken cancellationToken) =>
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
        })
        .CacheOutput(policyName: "Lists")
        .WithName("GetReviewsV2")
        .WithDescription("List reviews with standardized pagination. Returns PagedResult with metadata including HasPrevious and HasNext.");

        // Keyset (Cursor-based) Pagination - Optimized for deep pagination and large datasets
        group.MapGet("/cursor", async (AppDbContext db, int? afterId = null, int pageSize = 50) =>
        {
            // Validate pagination parameters
            pageSize = Math.Clamp(pageSize, 1, 100); // Max 100 items per page

            var query = db.Reviews.AsNoTracking();

            // Apply cursor filter if provided
            if (afterId.HasValue)
            {
                query = query.Where(r => r.Id > afterId.Value);
            }

            // Fetch one extra to determine if there are more results
            var allReviews = await query
                .TagWith("GET /reviews/cursor - Keyset pagination (optimized for deep pages)")
                .OrderBy(r => r.Id) // Required for consistent pagination and optimal index usage
                .Take(pageSize + 1)
                .Select(r => new ReviewListDto(
                    r.Id,
                    r.ProductId,
                    r.UserId,
                    r.Rating,
                    r.IsVerifiedPurchase))
                .ToListAsync();

            // ✅ OPTIMIZED: Detect hasMore BEFORE slicing (no allocation for extra item)
            var hasMore = allReviews.Count > pageSize;

            // ✅ FAST: Take() creates view of first pageSize items (O(1) vs RemoveAt O(n))
            var reviews = allReviews.Take(pageSize).ToList();

            return Results.Ok(new
            {
                Data = reviews,
                PageSize = pageSize,
                HasMore = hasMore,
                NextCursor = hasMore && reviews.Count > 0 ? reviews[^1].Id : (int?)null
            });
        })
        .CacheOutput("Lists")
        .WithName("GetReviewsCursor")
          .WithDescription("Keyset/cursor-based pagination - O(1) performance for any page depth. Use afterId parameter to continue from last record.");

        // Streaming - Get all reviews with optional filters using IAsyncEnumerable
        // Supports content negotiation: MessagePack, NDJSON, or JSON based on Accept header
        group.MapGet("/stream", (HttpContext context, AppDbContext db, int? productId = null, int? userId = null, int? minRating = null) =>
        {
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
                .Select(r => new ReviewListDto(
                    r.Id,
                    r.ProductId,
                    r.UserId,
                    r.Rating,
                    r.IsVerifiedPurchase))
                .AsAsyncEnumerable();

            // Content negotiation: return in client-preferred format (MessagePack, NDJSON, or JSON)
            return context.StreamAs(reviews);
        }).WithName("StreamReviews")
          .WithDescription("Stream all reviews with content negotiation (MessagePack, NDJSON, or JSON). Use Accept header to specify format. Supports filters: productId, userId, minRating")
          .Produces(StatusCodes.Status200OK);

        // NDJSON Export
        group.MapGet("/export/ndjson", async (HttpContext context, AppDbContext db, int? productId = null, int? userId = null, int? minRating = null, int limit = 50000, CancellationToken cancellationToken = default) =>
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
        }).WithName("ExportReviewsNdjson")
          .WithDescription("Export reviews as NDJSON. Supports filters: productId, userId, minRating. Max 50K items.")
          .Produces(StatusCodes.Status200OK);

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
        {
            var review = await CompiledQueries.GetReviewById(db, id);
            if (review is null) return Results.NotFound();

            return Results.Ok(new ReviewDto(
                review.Id,
                review.ProductId,
                review.UserId,
                review.Rating,
                review.Comment,
                review.CreatedDate,
                review.IsVerifiedPurchase));
        })
        .CacheOutput("Single");

        group.MapPost("/", async (Review review, AppDbContext db, IOutputCacheStore cache) =>
        {
            review.CreatedDate = DateTime.UtcNow;
            db.Reviews.Add(review);
            await db.SaveChangesAsync();

            // Invalidate caches after creating new review
            await cache.EvictByTagAsync("lists", default);

            return Results.Created($"/reviews/{review.Id}", review);
        });

        // Batch POST - Create multiple reviews
        group.MapPost("/bulk", async (List<Review> reviews, AppDbContext db, IOutputCacheStore cache) =>
        {
            if (reviews == null || reviews.Count == 0)
                return Results.BadRequest("Review list cannot be empty");

            var now = DateTime.UtcNow;
            foreach (var review in reviews)
            {
                review.CreatedDate = now;
            }

            db.Reviews.AddRange(reviews);
            await db.SaveChangesAsync();

            // Invalidate caches after bulk create
            await cache.EvictByTagAsync("lists", default);

            return Results.Created("/reviews/bulk", new
            {
                Count = reviews.Count,
                Message = $"Created {reviews.Count} reviews",
                ReviewIds = reviews.Select(r => r.Id).ToList()
            });
        }).WithName("BulkCreateReviews")
          .WithDescription("Create multiple reviews in a single transaction using AddRange");

        group.MapPut("/{id}", async (int id, Review inputReview, AppDbContext db, IOutputCacheStore cache) =>
        {
            var review = await db.Reviews.FindAsync(id);
            if (review is null) return Results.NotFound();

            review.Rating = inputReview.Rating;
            review.Comment = inputReview.Comment;
            review.IsVerifiedPurchase = inputReview.IsVerifiedPurchase;

            await db.SaveChangesAsync();

            // Invalidate caches after update
            await cache.EvictByTagAsync("lists", default);
            await cache.EvictByTagAsync("single", default);

            return Results.NoContent();
        });

        // Batch PUT - Update multiple reviews with streaming
        group.MapPut("/bulk", async (List<Review> reviews, AppDbContext db, ILogger<Program> logger, IOutputCacheStore cache) =>
        {
            if (reviews == null || reviews.Count == 0)
                return Results.BadRequest("Review list cannot be empty");

            // Create lookup dictionary for O(1) access
            var updateLookup = reviews.ToDictionary(r => r.Id);
            var reviewIds = updateLookup.Keys.ToList();

            using var transaction = await db.Database.BeginTransactionAsync();
            try
            {
                const int batchSize = 500;
                var batch = new List<Review>(batchSize);
                var updated = 0;

                // Stream entities instead of loading all at once
                await foreach (var existingReview in db.Reviews
                    .AsTracking()
                    .Where(r => reviewIds.Contains(r.Id))
                    .AsAsyncEnumerable())
                {
                    // Apply per-entity updates
                    var inputReview = updateLookup[existingReview.Id];
                    existingReview.Rating = inputReview.Rating;
                    existingReview.Comment = inputReview.Comment;
                    existingReview.IsVerifiedPurchase = inputReview.IsVerifiedPurchase;

                    batch.Add(existingReview);
                    updateLookup.Remove(existingReview.Id); // Track processed items

                    // Save and clear batch
                    if (batch.Count >= batchSize)
                    {
                        await db.SaveChangesAsync();
                        db.ChangeTracker.Clear(); // Critical: Free memory
                        updated += batch.Count;
                        batch.Clear();

                        logger.LogInformation("Processed batch: {Count}/{Total} reviews", updated, reviews.Count);
                    }
                }

                // Process remaining items
                if (batch.Count > 0)
                {
                    await db.SaveChangesAsync();
                    updated += batch.Count;
                }

                await transaction.CommitAsync();

                // Remaining items in updateLookup were not found
                var notFound = updateLookup.Keys.ToList();

                // Invalidate caches after bulk update
                await cache.EvictByTagAsync("lists", default);
                await cache.EvictByTagAsync("single", default);

                return Results.Ok(new
                {
                    Updated = updated,
                    NotFound = notFound,
                    Message = $"Updated {updated} reviews, {notFound.Count} not found"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                logger.LogError(ex, "Bulk update failed, rolled back");
                return Results.Problem("Bulk update failed: " + ex.Message);
            }
        }).WithName("BulkUpdateReviews")
          .WithDescription("Update multiple reviews using streaming with batching (constant memory ~5-10MB)");

        group.MapDelete("/{id}", async (int id, AppDbContext db, IOutputCacheStore cache) =>
        {
            if (await db.Reviews.FindAsync(id) is Review review)
            {
                db.Reviews.Remove(review);
                await db.SaveChangesAsync();

                // Invalidate caches after delete
                await cache.EvictByTagAsync("lists", default);
                await cache.EvictByTagAsync("single", default);

                return Results.NoContent();
            }
            return Results.NotFound();
        });

        // Batch DELETE - Delete multiple reviews by IDs
        group.MapDelete("/bulk", async ([FromBody] List<int> reviewIds, [FromServices] AppDbContext db, [FromServices] IOutputCacheStore cache) =>
        {
            if (reviewIds == null || reviewIds.Count == 0)
                return Results.BadRequest("Review ID list cannot be empty");

            // ExecuteDeleteAsync: Zero memory usage, direct SQL DELETE
            var deletedCount = await db.Reviews
                .Where(r => reviewIds.Contains(r.Id))
                .ExecuteDeleteAsync();

            if (deletedCount == 0)
                return Results.NotFound("No reviews found with the provided IDs");

            // Invalidate caches after bulk delete
            await cache.EvictByTagAsync("lists", default);
            await cache.EvictByTagAsync("single", default);

            return Results.Ok(new
            {
                Deleted = deletedCount,
                NotFound = reviewIds.Count - deletedCount,
                Message = $"Deleted {deletedCount} reviews, {reviewIds.Count - deletedCount} not found"
            });
        }).WithName("BulkDeleteReviews")
          .WithDescription("Delete multiple reviews by IDs without loading entities into memory (ExecuteDeleteAsync)");

        // ExecuteDelete - Delete old reviews for a product
        group.MapDelete("/product/{productId}/bulk-delete-old", async (int productId, int olderThanDays, AppDbContext db) =>
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
            var affectedRows = await db.Reviews
                .Where(r => r.ProductId == productId && r.CreatedDate < cutoffDate)
                .ExecuteDeleteAsync();

            return Results.Ok(new { AffectedRows = affectedRows, Message = $"Deleted {affectedRows} reviews older than {olderThanDays} days for product {productId}" });
        }).WithName("BulkDeleteOldReviewsForProduct")
          .WithDescription("Bulk delete old reviews for a specific product without loading entities into memory");
    }
}
