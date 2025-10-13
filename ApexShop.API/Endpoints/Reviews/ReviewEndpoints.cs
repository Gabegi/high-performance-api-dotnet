using ApexShop.API.DTOs;
using ApexShop.API.Queries;
using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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

            var totalCount = await db.Reviews.CountAsync();

            return Results.Ok(new
            {
                Data = reviews,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        });

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
            var reviews = await query
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

            var hasMore = reviews.Count > pageSize;
            if (hasMore)
            {
                reviews.RemoveAt(reviews.Count - 1); // Remove the extra item
            }

            return Results.Ok(new
            {
                Data = reviews,
                PageSize = pageSize,
                HasMore = hasMore,
                NextCursor = hasMore && reviews.Count > 0 ? reviews[^1].Id : (int?)null
            });
        }).WithName("GetReviewsCursor")
          .WithDescription("Keyset/cursor-based pagination - O(1) performance for any page depth. Use afterId parameter to continue from last record.");

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
            await CompiledQueries.GetReviewById(db, id)
                is ReviewDto review ? Results.Ok(review) : Results.NotFound());

        group.MapPost("/", async (Review review, AppDbContext db) =>
        {
            review.CreatedDate = DateTime.UtcNow;
            db.Reviews.Add(review);
            await db.SaveChangesAsync();
            return Results.Created($"/reviews/{review.Id}", review);
        });

        // Batch POST - Create multiple reviews
        group.MapPost("/bulk", async (List<Review> reviews, AppDbContext db) =>
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

            return Results.Created("/reviews/bulk", new
            {
                Count = reviews.Count,
                Message = $"Created {reviews.Count} reviews",
                ReviewIds = reviews.Select(r => r.Id).ToList()
            });
        }).WithName("BulkCreateReviews")
          .WithDescription("Create multiple reviews in a single transaction using AddRange");

        group.MapPut("/{id}", async (int id, Review inputReview, AppDbContext db) =>
        {
            var review = await db.Reviews.FindAsync(id);
            if (review is null) return Results.NotFound();

            review.Rating = inputReview.Rating;
            review.Comment = inputReview.Comment;
            review.IsVerifiedPurchase = inputReview.IsVerifiedPurchase;

            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Batch PUT - Update multiple reviews with streaming
        group.MapPut("/bulk", async (List<Review> reviews, AppDbContext db, ILogger<Program> logger) =>
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

        group.MapDelete("/{id}", async (int id, AppDbContext db) =>
        {
            if (await db.Reviews.FindAsync(id) is Review review)
            {
                db.Reviews.Remove(review);
                await db.SaveChangesAsync();
                return Results.NoContent();
            }
            return Results.NotFound();
        });

        // Batch DELETE - Delete multiple reviews by IDs
        group.MapDelete("/bulk", async (List<int> reviewIds, AppDbContext db) =>
        {
            if (reviewIds == null || reviewIds.Count == 0)
                return Results.BadRequest("Review ID list cannot be empty");

            // ExecuteDeleteAsync: Zero memory usage, direct SQL DELETE
            var deletedCount = await db.Reviews
                .Where(r => reviewIds.Contains(r.Id))
                .ExecuteDeleteAsync();

            if (deletedCount == 0)
                return Results.NotFound("No reviews found with the provided IDs");

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
