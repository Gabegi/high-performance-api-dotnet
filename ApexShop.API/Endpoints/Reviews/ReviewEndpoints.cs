using ApexShop.API.DTOs;
using ApexShop.Domain.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.API.Endpoints.Reviews;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reviews").WithTags("Reviews");

        group.MapGet("/", async (AppDbContext db) =>
            await db.Reviews
                .AsNoTracking()
                .Select(r => new ReviewListDto(
                    r.Id,
                    r.ProductId,
                    r.UserId,
                    r.Rating,
                    r.IsVerifiedPurchase))
                .ToListAsync());

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
            await db.Reviews
                .AsNoTracking()
                .Where(r => r.Id == id)
                .Select(r => new ReviewDto(
                    r.Id,
                    r.ProductId,
                    r.UserId,
                    r.Rating,
                    r.Comment,
                    r.CreatedDate,
                    r.IsVerifiedPurchase))
                .FirstOrDefaultAsync()
                is ReviewDto review ? Results.Ok(review) : Results.NotFound());

        group.MapPost("/", async (Review review, AppDbContext db) =>
        {
            review.CreatedDate = DateTime.UtcNow;
            db.Reviews.Add(review);
            await db.SaveChangesAsync();
            return Results.Created($"/reviews/{review.Id}", review);
        });

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
    }
}
