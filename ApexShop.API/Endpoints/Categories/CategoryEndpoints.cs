using ApexShop.API.DTOs;
using ApexShop.Domain.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.API.Endpoints.Categories;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/categories").WithTags("Categories");

        group.MapGet("/", async (AppDbContext db, int page = 1, int pageSize = 50) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var categories = await db.Categories
                .AsNoTracking()
                .TagWith("GET /categories - List categories with pagination")
                .OrderBy(c => c.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CategoryListDto(
                    c.Id,
                    c.Name,
                    c.Description))
                .ToListAsync();

            var totalCount = await db.Categories.CountAsync();

            return Results.Ok(new
            {
                Data = categories,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        });

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
            await db.Categories
                .AsNoTracking()
                .TagWith("GET /categories/{id} - Get category by ID")
                .Where(c => c.Id == id)
                .Select(c => new CategoryDto(
                    c.Id,
                    c.Name,
                    c.Description,
                    c.CreatedDate))
                .FirstOrDefaultAsync()
                is CategoryDto category ? Results.Ok(category) : Results.NotFound());

        group.MapPost("/", async (Category category, AppDbContext db) =>
        {
            category.CreatedDate = DateTime.UtcNow;
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            return Results.Created($"/categories/{category.Id}", category);
        });

        group.MapPut("/{id}", async (int id, Category inputCategory, AppDbContext db) =>
        {
            var category = await db.Categories.FindAsync(id);
            if (category is null) return Results.NotFound();

            category.Name = inputCategory.Name;
            category.Description = inputCategory.Description;

            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapDelete("/{id}", async (int id, AppDbContext db) =>
        {
            if (await db.Categories.FindAsync(id) is Category category)
            {
                db.Categories.Remove(category);
                await db.SaveChangesAsync();
                return Results.NoContent();
            }
            return Results.NotFound();
        });
    }
}
