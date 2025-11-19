using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using ApexShop.Application.DTOs;

namespace ApexShop.Application.Features.Reviews.Handlers;

/// <summary>
/// Handler for GET /reviews/{id} - Retrieve a single review by ID
/// Uses compiled queries for optimal performance.
/// </summary>
public static class GetReviewById
{
    /// <summary>
    /// GET /{id} - Get review by ID
    /// Uses compiled queries for maximum performance.
    /// </summary>
    public static async Task<IResult> GetReviewByIdHandler(
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
