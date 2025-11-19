using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using ApexShop.Application.DTOs;
using ApexShop.API.Models.Pagination;
using ApexShop.API.Extensions;
using ApexShop.API.JsonContext;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace ApexShop.Application.Features.Categories.Handlers;

/// <summary>
/// Handlers for updating categories.
/// Supports single and bulk update operations with streaming.
/// </summary>
public static class UpdateCategory
{
    /// <summary>
    /// PUT /{id} - Update a single category
    /// Returns 204 No Content on success, 404 if not found.
    /// </summary>
    public static async Task<IResult> UpdateCategoryHandler(
        int id,
        Category inputCategory,
        AppDbContext db,
        IOutputCacheStore cache)
    {
        var category = await db.Categories.FindAsync(id);
        if (category is null)
            return Results.NotFound();

        category.Name = inputCategory.Name;
        category.Description = inputCategory.Description;

        await db.SaveChangesAsync();
        await cache.EvictByTagAsync("categories", default);

        return Results.NoContent();
    }

    /// <summary>
    /// PUT /bulk - Update multiple categories with streaming
    /// Uses HashSet for O(1) lookups and batching for memory efficiency.
    /// Returns updated count and list of IDs not found.
    /// </summary>
    public static async Task<IResult> UpdateCategoriesBulkHandler(
        List<Category> categories,
        AppDbContext db,
        IOutputCacheStore cache,
        ILogger<Program> logger)
    {
        if (categories == null || categories.Count == 0)
            return Results.BadRequest("Category list cannot be empty");

        var updateLookup = categories.ToDictionary(c => c.Id);
        var categoryIds = updateLookup.Keys.ToHashSet();

        using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            const int batchSize = 500;
            var batch = new List<Category>(batchSize);
            var updated = 0;

            await foreach (var existingCategory in db.Categories
                .AsTracking()
                .Where(c => categoryIds.Contains(c.Id))
                .AsAsyncEnumerable())
            {
                var inputCategory = updateLookup[existingCategory.Id];
                existingCategory.Name = inputCategory.Name;
                existingCategory.Description = inputCategory.Description;

                batch.Add(existingCategory);
                updateLookup.Remove(existingCategory.Id);

                if (batch.Count >= batchSize)
                {
                    await db.SaveChangesAsync();
                    db.ChangeTracker.Clear();
                    updated += batch.Count;
                    batch.Clear();

                    logger.LogInformation("Processed batch: {Count}/{Total} categories", updated, categories.Count);
                }
            }

            if (batch.Count > 0)
            {
                await db.SaveChangesAsync();
                updated += batch.Count;
            }

            await transaction.CommitAsync();
            await cache.EvictByTagAsync("categories", default);

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
