using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.API.Endpoints.Categories;

/// <summary>
/// DELETE endpoints for removing categories.
/// - DELETE /{id} - Delete a single category
/// - DELETE /bulk - Delete multiple categories by IDs
/// </summary>
public static class DeleteCategoryEndpoint
{
    public static RouteGroupBuilder MapDeleteCategory(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id}", DeleteCategoryHandler)
            .WithName("DeleteCategory")
            .WithDescription("Delete a single category")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/bulk", DeleteCategoriesBulkHandler)
            .WithName("BulkDeleteCategories")
            .WithDescription("Delete multiple categories by IDs without loading entities into memory (ExecuteDeleteAsync)")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    /// <summary>
    /// DELETE /{id} - Delete a single category
    /// </summary>
    private static async Task<IResult> DeleteCategoryHandler(
        int id,
        AppDbContext db)
    {
        if (await db.Categories.FindAsync(id) is Category category)
        {
            db.Categories.Remove(category);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }

        return Results.NotFound();
    }

    /// <summary>
    /// DELETE /bulk - Delete multiple categories by IDs
    /// ✅ OPTIMIZED: Use HashSet for O(1) Contains() lookups instead of List O(n)
    /// </summary>
    private static async Task<IResult> DeleteCategoriesBulkHandler(
        [FromBody] List<int> categoryIds,
        [FromServices] AppDbContext db)
    {
        if (categoryIds == null || categoryIds.Count == 0)
            return Results.BadRequest("Category ID list cannot be empty");

        // ✅ OPTIMIZED: Convert to HashSet for O(1) Contains() in WHERE clause
        var categoryIdSet = categoryIds.ToHashSet();

        // ExecuteDeleteAsync: Zero memory usage, direct SQL DELETE
        // ✅ FAST: HashSet.Contains() is O(1) vs List.Contains() O(n)
        var deletedCount = await db.Categories
            .Where(c => categoryIdSet.Contains(c.Id))
            .ExecuteDeleteAsync();

        if (deletedCount == 0)
            return Results.NotFound("No categories found with the provided IDs");

        return Results.Ok(new
        {
            Deleted = deletedCount,
            NotFound = categoryIds.Count - deletedCount,
            Message = $"Deleted {deletedCount} categories, {categoryIds.Count - deletedCount} not found"
        });
    }
}
