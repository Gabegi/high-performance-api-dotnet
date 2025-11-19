using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.OutputCaching;

namespace ApexShop.API.Endpoints.Reviews;

/// <summary>
/// POST endpoints for creating reviews.
/// - POST / - Create a single review
/// - POST /bulk - Create multiple reviews in a single transaction
/// </summary>
public static class CreateReviewEndpoint
{
    public static RouteGroupBuilder MapCreateReview(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateReviewHandler)
            .WithName("CreateReview")
            .WithDescription("Create a single review")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/bulk", CreateReviewsBulkHandler)
            .WithName("BulkCreateReviews")
            .WithDescription("Create multiple reviews in a single transaction using AddRange")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        return group;
    }

    /// <summary>
    /// POST / - Create a single review
    /// </summary>
    private static async Task<IResult> CreateReviewHandler(
        Review review,
        AppDbContext db,
        IOutputCacheStore cache)
    {
        review.CreatedDate = DateTime.UtcNow;
        db.Reviews.Add(review);
        await db.SaveChangesAsync();

        // Invalidate caches after creating new review
        await cache.EvictByTagAsync("lists", default);

        return Results.Created($"/reviews/{review.Id}", review);
    }

    /// <summary>
    /// POST /bulk - Create multiple reviews
    /// </summary>
    private static async Task<IResult> CreateReviewsBulkHandler(
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

        // Invalidate caches after bulk create
        await cache.EvictByTagAsync("lists", default);

        return Results.Created("/reviews/bulk", new
        {
            Count = reviews.Count,
            Message = $"Created {reviews.Count} reviews",
            ReviewIds = reviews.Select(r => r.Id).ToList()
        });
    }
}
