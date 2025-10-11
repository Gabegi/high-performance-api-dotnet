using ApexShop.API.DTOs;
using ApexShop.API.Queries;
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
            await CompiledQueries.GetCategoryById(db, id)
                is CategoryDto category ? Results.Ok(category) : Results.NotFound());

        group.MapPost("/", async (Category category, AppDbContext db) =>
        {
            category.CreatedDate = DateTime.UtcNow;
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            return Results.Created($"/categories/{category.Id}", category);
        });

        // Batch POST - Create multiple categories
        group.MapPost("/bulk", async (List<Category> categories, AppDbContext db) =>
        {
            if (categories == null || categories.Count == 0)
                return Results.BadRequest("Category list cannot be empty");

            var now = DateTime.UtcNow;
            foreach (var category in categories)
            {
                category.CreatedDate = now;
            }

            db.Categories.AddRange(categories);
            await db.SaveChangesAsync();

            return Results.Created("/categories/bulk", new
            {
                Count = categories.Count,
                Message = $"Created {categories.Count} categories",
                CategoryIds = categories.Select(c => c.Id).ToList()
            });
        }).WithName("BulkCreateCategories")
          .WithDescription("Create multiple categories in a single transaction using AddRange");

        group.MapPut("/{id}", async (int id, Category inputCategory, AppDbContext db) =>
        {
            var category = await db.Categories.FindAsync(id);
            if (category is null) return Results.NotFound();

            category.Name = inputCategory.Name;
            category.Description = inputCategory.Description;

            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Batch PUT - Update multiple categories
        group.MapPut("/bulk", async (List<Category> categories, AppDbContext db) =>
        {
            if (categories == null || categories.Count == 0)
                return Results.BadRequest("Category list cannot be empty");

            var categoryIds = categories.Select(c => c.Id).ToList();
            var existingCategories = await db.Categories
                .AsTracking()
                .Where(c => categoryIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);

            var notFound = new List<int>();
            var updated = 0;

            foreach (var inputCategory in categories)
            {
                if (existingCategories.TryGetValue(inputCategory.Id, out var existingCategory))
                {
                    existingCategory.Name = inputCategory.Name;
                    existingCategory.Description = inputCategory.Description;
                    updated++;
                }
                else
                {
                    notFound.Add(inputCategory.Id);
                }
            }

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                Updated = updated,
                NotFound = notFound,
                Message = $"Updated {updated} categories, {notFound.Count} not found"
            });
        }).WithName("BulkUpdateCategories")
          .WithDescription("Update multiple categories in a single transaction");

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

        // Batch DELETE - Delete multiple categories by IDs
        group.MapDelete("/bulk", async (List<int> categoryIds, AppDbContext db) =>
        {
            if (categoryIds == null || categoryIds.Count == 0)
                return Results.BadRequest("Category ID list cannot be empty");

            var categories = await db.Categories
                .AsTracking()
                .Where(c => categoryIds.Contains(c.Id))
                .ToListAsync();

            if (categories.Count == 0)
                return Results.NotFound("No categories found with the provided IDs");

            db.Categories.RemoveRange(categories);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                Deleted = categories.Count,
                NotFound = categoryIds.Count - categories.Count,
                Message = $"Deleted {categories.Count} categories, {categoryIds.Count - categories.Count} not found"
            });
        }).WithName("BulkDeleteCategories")
          .WithDescription("Delete multiple categories by IDs in a single transaction using RemoveRange");
    }
}
