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

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
            await db.Reviews
                .AsNoTracking()
                .TagWith("GET /reviews/{id} - Get review by ID")
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
