using ApexShop.API.DTOs;
using ApexShop.API.Queries;
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

        // Batch PUT - Update multiple reviews
        group.MapPut("/bulk", async (List<Review> reviews, AppDbContext db) =>
        {
            if (reviews == null || reviews.Count == 0)
                return Results.BadRequest("Review list cannot be empty");

            var reviewIds = reviews.Select(r => r.Id).ToList();
            var existingReviews = await db.Reviews
                .Where(r => reviewIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id);

            var notFound = new List<int>();
            var updated = 0;

            foreach (var inputReview in reviews)
            {
                if (existingReviews.TryGetValue(inputReview.Id, out var existingReview))
                {
                    existingReview.Rating = inputReview.Rating;
                    existingReview.Comment = inputReview.Comment;
                    existingReview.IsVerifiedPurchase = inputReview.IsVerifiedPurchase;
                    updated++;
                }
                else
                {
                    notFound.Add(inputReview.Id);
                }
            }

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                Updated = updated,
                NotFound = notFound,
                Message = $"Updated {updated} reviews, {notFound.Count} not found"
            });
        }).WithName("BulkUpdateReviews")
          .WithDescription("Update multiple reviews in a single transaction");

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

            var reviews = await db.Reviews
                .Where(r => reviewIds.Contains(r.Id))
                .ToListAsync();

            if (reviews.Count == 0)
                return Results.NotFound("No reviews found with the provided IDs");

            db.Reviews.RemoveRange(reviews);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                Deleted = reviews.Count,
                NotFound = reviewIds.Count - reviews.Count,
                Message = $"Deleted {reviews.Count} reviews, {reviewIds.Count - reviews.Count} not found"
            });
        }).WithName("BulkDeleteReviews")
          .WithDescription("Delete multiple reviews by IDs in a single transaction using RemoveRange");

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
