using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.API.Endpoints.Reviews;

/// <summary>
/// DELETE endpoints for removing reviews.
/// - DELETE /{id} - Delete a single review
/// - DELETE /bulk - Delete multiple reviews by IDs
/// - DELETE /product/{productId}/bulk-delete-old - Bulk delete old reviews for a product
/// </summary>
public static class DeleteReviewEndpoint
{
    public static RouteGroupBuilder MapDeleteReview(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id}", DeleteReviewHandler)
            .WithName("DeleteReview")
            .WithDescription("Delete a single review")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/bulk", DeleteReviewsBulkHandler)
            .WithName("BulkDeleteReviews")
            .WithDescription("Delete multiple reviews by IDs without loading entities into memory (ExecuteDeleteAsync)")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/product/{productId}/bulk-delete-old", DeleteOldReviewsForProductHandler)
            .WithName("BulkDeleteOldReviewsForProduct")
            .WithDescription("Bulk delete old reviews for a specific product without loading entities into memory")
            .Produces(StatusCodes.Status200OK);

        return group;
    }

    /// <summary>
    /// DELETE /{id} - Delete a single review
    /// </summary>
    private static async Task<IResult> DeleteReviewHandler(
        int id,
        AppDbContext db,
        IOutputCacheStore cache)
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
    }

    /// <summary>
    /// DELETE /bulk - Delete multiple reviews by IDs
    /// ✅ OPTIMIZED: Use HashSet for O(1) Contains() lookups instead of List O(n)
    /// </summary>
    private static async Task<IResult> DeleteReviewsBulkHandler(
        [FromBody] List<int> reviewIds,
        [FromServices] AppDbContext db,
        [FromServices] IOutputCacheStore cache)
    {
        if (reviewIds == null || reviewIds.Count == 0)
            return Results.BadRequest("Review ID list cannot be empty");

        // ✅ OPTIMIZED: Convert to HashSet for O(1) Contains() in WHERE clause
        var reviewIdSet = reviewIds.ToHashSet();

        // ExecuteDeleteAsync: Zero memory usage, direct SQL DELETE
        // ✅ FAST: HashSet.Contains() is O(1) vs List.Contains() O(n)
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
    /// DELETE /product/{productId}/bulk-delete-old - Bulk delete old reviews for a product
    /// ExecuteDeleteAsync: Zero memory usage, direct SQL DELETE
    /// </summary>
    private static async Task<IResult> DeleteOldReviewsForProductHandler(
        int productId,
        int olderThanDays,
        AppDbContext db)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
        var affectedRows = await db.Reviews
            .Where(r => r.ProductId == productId && r.CreatedDate < cutoffDate)
            .ExecuteDeleteAsync();

        return Results.Ok(new
        {
            AffectedRows = affectedRows,
            Message = $"Deleted {affectedRows} reviews older than {olderThanDays} days for product {productId}"
        });
    }
}
