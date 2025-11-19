using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.API.Endpoints.Categories;

/// <summary>
/// PUT endpoints for updating categories.
/// - PUT /{id} - Update a single category
/// - PUT /bulk - Update multiple categories with streaming and batching
/// </summary>
public static class UpdateCategoryEndpoint
{
    public static RouteGroupBuilder MapUpdateCategory(this RouteGroupBuilder group)
    {
        group.MapPut("/{id}", UpdateCategoryHandler)
            .WithName("UpdateCategory")
            .WithDescription("Update a single category")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/bulk", UpdateCategoriesBulkHandler)
            .WithName("BulkUpdateCategories")
            .WithDescription("Update multiple categories using streaming with batching (constant memory ~5-10MB)")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        return group;
    }

    /// <summary>
    /// PUT /{id} - Update a single category
    /// </summary>
    private static async Task<IResult> UpdateCategoryHandler(
        int id,
        Category inputCategory,
        AppDbContext db)
    {
        var category = await db.Categories.FindAsync(id);
        if (category is null)
            return Results.NotFound();

        category.Name = inputCategory.Name;
        category.Description = inputCategory.Description;

        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    /// <summary>
    /// PUT /bulk - Update multiple categories with streaming
    /// ✅ OPTIMIZED: Use HashSet for O(1) Contains() lookups instead of List O(n)
    /// </summary>
    private static async Task<IResult> UpdateCategoriesBulkHandler(
        List<Category> categories,
        AppDbContext db,
        ILogger<Program> logger)
    {
        if (categories == null || categories.Count == 0)
            return Results.BadRequest("Category list cannot be empty");

        // Create lookup dictionary for O(1) access
        var updateLookup = categories.ToDictionary(c => c.Id);
        // ✅ OPTIMIZED: Use HashSet instead of List for O(1) Contains() per row
        var categoryIds = updateLookup.Keys.ToHashSet();

        using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            const int batchSize = 500;
            var batch = new List<Category>(batchSize);
            var updated = 0;

            // Stream entities instead of loading all at once
            // ✅ FAST: HashSet.Contains() is O(1) vs List.Contains() O(n)
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
    }
}
