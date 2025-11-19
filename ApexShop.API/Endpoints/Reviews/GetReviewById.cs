using ApexShop.API.DTOs;
using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using Microsoft.AspNetCore.OutputCaching;

namespace ApexShop.API.Endpoints.Reviews;

/// <summary>
/// GET /{id} endpoint for retrieving a single review by ID.
/// Uses compiled queries for optimal performance.
/// </summary>
public static class GetReviewByIdEndpoint
{
    public static RouteGroupBuilder MapGetReviewById(this RouteGroupBuilder group)
    {
        group.MapGet("/{id}", GetReviewByIdHandler)
            .CacheOutput("Single")
            .WithName("GetReviewById")
            .WithDescription("Get a single review by ID");

        return group;
    }

    /// <summary>
    /// GET /{id} - Retrieve a single review by ID
    /// </summary>
    private static async Task<IResult> GetReviewByIdHandler(
        int id,
        AppDbContext db)
    {
        var review = await CompiledQueries.GetReviewById(db, id);
        if (review is null)
            return Results.NotFound();

        return Results.Ok(new ReviewDto(
            review.Id,
            review.ProductId,
            review.UserId,
            review.Rating,
            review.Comment,
            review.CreatedDate,
            review.IsVerifiedPurchase));
    }
}
