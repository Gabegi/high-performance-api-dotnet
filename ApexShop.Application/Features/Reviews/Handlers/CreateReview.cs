using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.OutputCaching;

namespace ApexShop.Application.Features.Reviews.Handlers;

/// <summary>
/// Handlers for creating reviews (single and bulk operations).
/// </summary>
public static class CreateReview
{
    /// <summary>
    /// POST / - Create a single review
    /// </summary>
    public static async Task<IResult> CreateReviewHandler(
        Review review,
        AppDbContext db,
        IOutputCacheStore cache)
    {
        review.CreatedDate = DateTime.UtcNow;
        db.Reviews.Add(review);
        await db.SaveChangesAsync();

        // Invalidate list caches after creating new review
        await cache.EvictByTagAsync("lists", default);

        return Results.Created($"/reviews/{review.Id}", review);
    }

    /// <summary>
    /// POST /bulk - Create multiple reviews in a single transaction
    /// </summary>
    public static async Task<IResult> CreateReviewsBulkHandler(
        List<Review> reviews,
        AppDbContext db,
        IOutputCacheStore cache)
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

        // Invalidate list caches after bulk create
        await cache.EvictByTagAsync("lists", default);

        return Results.Ok(new
        {
            Created = reviews.Count,
            Message = $"Successfully created {reviews.Count} reviews"
        });
    }
}
