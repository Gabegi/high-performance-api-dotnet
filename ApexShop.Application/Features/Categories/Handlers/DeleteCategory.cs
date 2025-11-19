using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using ApexShop.API.DTOs;
using ApexShop.API.Models.Pagination;
using ApexShop.API.Extensions;
using ApexShop.API.JsonContext;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace ApexShop.Application.Features.Categories.Handlers;

/// <summary>
/// Handlers for deleting categories.
/// Supports single and bulk deletion operations.
/// </summary>
public static class DeleteCategory
{
    /// <summary>
    /// DELETE /{id} - Delete a single category
    /// Returns 204 No Content on success, 404 if not found.
    /// </summary>
    public static async Task<IResult> DeleteCategoryHandler(
        int id,
        AppDbContext db,
        IOutputCacheStore cache)
    {
        if (await db.Categories.FindAsync(id) is Category category)
        {
            db.Categories.Remove(category);
            await db.SaveChangesAsync();
            await cache.EvictByTagAsync("categories", default);
            return Results.NoContent();
        }

        return Results.NotFound();
    }

    /// <summary>
    /// DELETE /bulk - Delete multiple categories by IDs
    /// Uses ExecuteDeleteAsync for efficient bulk deletion without loading entities.
    /// Uses HashSet for O(1) Contains() lookups.
    /// </summary>
    public static async Task<IResult> DeleteCategoriesBulkHandler(
        List<int> categoryIds,
        AppDbContext db,
        IOutputCacheStore cache)
    {
        if (categoryIds == null || categoryIds.Count == 0)
            return Results.BadRequest("Category ID list cannot be empty");

        var categoryIdSet = categoryIds.ToHashSet();

        var deletedCount = await db.Categories
            .Where(c => categoryIdSet.Contains(c.Id))
            .ExecuteDeleteAsync();

        if (deletedCount == 0)
            return Results.NotFound("No categories found with the provided IDs");

        await cache.EvictByTagAsync("categories", default);

        return Results.Ok(new
        {
            Deleted = deletedCount,
            NotFound = categoryIds.Count - deletedCount,
            Message = $"Deleted {deletedCount} categories, {categoryIds.Count - deletedCount} not found"
        });
    }
}
