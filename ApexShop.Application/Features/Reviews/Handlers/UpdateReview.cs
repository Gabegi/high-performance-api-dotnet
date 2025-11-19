using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.Application.Features.Reviews.Handlers;

/// <summary>
/// Handlers for updating reviews (single and bulk operations).
/// </summary>
public static class UpdateReview
{
    /// <summary>
    /// PUT /{id} - Update a single review
    /// </summary>
    public static async Task<IResult> UpdateReviewHandler(
        int id,
        Review inputReview,
        AppDbContext db,
        IOutputCacheStore cache)
    {
        var review = await db.Reviews.FindAsync(id);
        if (review is null)
            return Results.NotFound();

        review.Rating = inputReview.Rating;
        review.Comment = inputReview.Comment;

        await db.SaveChangesAsync();

        // Invalidate caches after update
        await cache.EvictByTagAsync("lists", default);
        await cache.EvictByTagAsync("single", default);

        return Results.Ok(review);
    }

    /// <summary>
    /// PUT /bulk - Update multiple reviews with streaming and batching
    /// Uses HashSet for O(1) Contains() lookups instead of List O(n)
    /// Processes reviews in batches of 500 items to manage memory efficiently.
    /// </summary>
    public static async Task<IResult> UpdateReviewsBulkHandler(
        List<Review> reviews,
        AppDbContext db,
        ILogger logger,
        IOutputCacheStore cache)
    {
        if (reviews == null || reviews.Count == 0)
            return Results.BadRequest("Review list cannot be empty");

        // Create lookup dictionary for O(1) access (input data - unavoidable memory usage)
        var updateLookup = reviews.ToDictionary(r => r.Id);
        // Use HashSet instead of List for O(1) Contains() per row
        var reviewIds = updateLookup.Keys.ToHashSet();

        using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            const int batchSize = 500;
            var batch = new List<Review>(batchSize);
            var updated = 0;

            // Stream entities instead of loading all at once
            // FAST: HashSet.Contains() is O(1) vs List.Contains() O(n)
            await foreach (var existingReview in db.Reviews
                .AsTracking()
                .Where(r => reviewIds.Contains(r.Id))
                .AsAsyncEnumerable())
            {
                // Apply per-entity updates
                var inputReview = updateLookup[existingReview.Id];
                existingReview.Rating = inputReview.Rating;
                existingReview.Comment = inputReview.Comment;

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
    }
}
