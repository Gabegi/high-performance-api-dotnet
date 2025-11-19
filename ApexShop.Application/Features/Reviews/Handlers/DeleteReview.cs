using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.Application.Features.Reviews.Handlers;

/// <summary>
/// Handlers for deleting reviews (single, bulk, and specialized operations).
/// </summary>
public static class DeleteReview
{
    /// <summary>
    /// DELETE /{id} - Delete a single review by ID
    /// </summary>
    public static async Task<IResult> DeleteReviewHandler(
        int id,
        AppDbContext db,
        IOutputCacheStore cache)
    {
        var review = await db.Reviews.FindAsync(id);
        if (review is null)
            return Results.NotFound();

        db.Reviews.Remove(review);
        await db.SaveChangesAsync();

        // Invalidate caches after delete
        await cache.EvictByTagAsync("lists", default);
        await cache.EvictByTagAsync("single", default);

        return Results.NoContent();
    }

    /// <summary>
    /// DELETE /bulk - Delete multiple reviews by IDs
    /// Uses HashSet for O(1) Contains() lookups instead of List O(n)
    /// ExecuteDeleteAsync: Zero memory usage, direct SQL DELETE
    /// </summary>
    public static async Task<IResult> DeleteReviewsBulkHandler(
        List<int> reviewIds,
        AppDbContext db,
        IOutputCacheStore cache)
    {
        if (reviewIds == null || reviewIds.Count == 0)
            return Results.BadRequest("Review ID list cannot be empty");

        // Convert to HashSet for O(1) Contains() in WHERE clause
        var reviewIdSet = reviewIds.ToHashSet();

        // ExecuteDeleteAsync: Zero memory usage, direct SQL DELETE
        // FAST: HashSet.Contains() is O(1) vs List.Contains() O(n)
        var deletedCount = await db.Reviews
            .Where(r => reviewIdSet.Contains(r.Id))
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
    }

    /// <summary>
    /// DELETE /product/{productId}/bulk-delete-old - Delete old reviews for a specific product
    /// Uses ExecuteDeleteAsync for zero-memory bulk deletion.
    /// Deletes reviews older than the specified date threshold.
    /// </summary>
    public static async Task<IResult> DeleteOldReviewsForProductHandler(
        int productId,
        AppDbContext db,
        IOutputCacheStore cache,
        DateTime? olderThan = null)
    {
        // Default to reviews older than 1 year if not specified
        var threshold = olderThan ?? DateTime.UtcNow.AddYears(-1);

        // ExecuteDeleteAsync: Zero memory usage, direct SQL DELETE
        var deletedCount = await db.Reviews
            .Where(r => r.ProductId == productId && r.CreatedDate < threshold)
            .ExecuteDeleteAsync();

        if (deletedCount == 0)
            return Results.NotFound($"No old reviews found for product {productId} older than {threshold:yyyy-MM-dd}");

        // Invalidate caches after bulk delete
        await cache.EvictByTagAsync("lists", default);
        await cache.EvictByTagAsync("single", default);

        return Results.Ok(new
        {
            ProductId = productId,
            Deleted = deletedCount,
            Threshold = threshold,
            Message = $"Deleted {deletedCount} reviews for product {productId} older than {threshold:yyyy-MM-dd}"
        });
    }
}
