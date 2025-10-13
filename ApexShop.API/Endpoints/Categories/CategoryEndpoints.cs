using ApexShop.API.DTOs;
using ApexShop.API.Queries;
using ApexShop.Infrastructure.Entities;
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

        // Batch PUT - Update multiple categories with streaming
        group.MapPut("/bulk", async (List<Category> categories, AppDbContext db, ILogger<Program> logger) =>
        {
            if (categories == null || categories.Count == 0)
                return Results.BadRequest("Category list cannot be empty");

            // Create lookup dictionary for O(1) access
            var updateLookup = categories.ToDictionary(c => c.Id);
            var categoryIds = updateLookup.Keys.ToList();

            using var transaction = await db.Database.BeginTransactionAsync();
            try
            {
                const int batchSize = 500;
                var batch = new List<Category>(batchSize);
                var updated = 0;

                // Stream entities instead of loading all at once
                await foreach (var existingCategory in db.Categories
                    .AsTracking()
                    .Where(c => categoryIds.Contains(c.Id))
                    .AsAsyncEnumerable())
                {
                    // Apply per-entity updates
                    var inputCategory = updateLookup[existingCategory.Id];
                    existingCategory.Name = inputCategory.Name;
                    existingCategory.Description = inputCategory.Description;

                    batch.Add(existingCategory);
                    updateLookup.Remove(existingCategory.Id); // Track processed items

                    // Save and clear batch
                    if (batch.Count >= batchSize)
                    {
                        await db.SaveChangesAsync();
                        db.ChangeTracker.Clear(); // Critical: Free memory
                        updated += batch.Count;
                        batch.Clear();

                        logger.LogInformation("Processed batch: {Count}/{Total} categories", updated, categories.Count);
                    }
                }

                // Process remaining items
                if (batch.Count > 0)
                {
                    await db.SaveChangesAsync();
                    updated += batch.Count;
                }

                await transaction.CommitAsync();

                // Remaining items in updateLookup were not found
                var notFound = updateLookup.Keys.ToList();

                return Results.Ok(new
                {
                    Updated = updated,
                    NotFound = notFound,
                    Message = $"Updated {updated} categories, {notFound.Count} not found"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                logger.LogError(ex, "Bulk update failed, rolled back");
                return Results.Problem("Bulk update failed: " + ex.Message);
            }
        }).WithName("BulkUpdateCategories")
          .WithDescription("Update multiple categories using streaming with batching (constant memory ~5-10MB)");

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

            // ExecuteDeleteAsync: Zero memory usage, direct SQL DELETE
            var deletedCount = await db.Categories
                .Where(c => categoryIds.Contains(c.Id))
                .ExecuteDeleteAsync();

            if (deletedCount == 0)
                return Results.NotFound("No categories found with the provided IDs");

            return Results.Ok(new
            {
                Deleted = deletedCount,
                NotFound = categoryIds.Count - deletedCount,
                Message = $"Deleted {deletedCount} categories, {categoryIds.Count - deletedCount} not found"
            });
        }).WithName("BulkDeleteCategories")
          .WithDescription("Delete multiple categories by IDs without loading entities into memory (ExecuteDeleteAsync)");
    }
}
