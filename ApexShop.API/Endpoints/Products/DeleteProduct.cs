using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.API.Endpoints.Products;

/// <summary>
/// DELETE endpoints for removing products.
/// - DELETE /{id} - Delete a single product
/// - DELETE /bulk - Delete multiple products by IDs
/// </summary>
public static class DeleteProductEndpoint
{
    public static RouteGroupBuilder MapDeleteProduct(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id}", DeleteProductHandler)
            .WithName("DeleteProduct")
            .WithDescription("Delete a single product")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/bulk", DeleteProductsBulkHandler)
            .WithName("BulkDeleteProducts")
            .WithDescription("Delete multiple products by IDs without loading entities into memory (ExecuteDeleteAsync)")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    /// <summary>
    /// DELETE /{id} - Delete a single product
    /// </summary>
    private static async Task<IResult> DeleteProductHandler(
        int id,
        AppDbContext db,
        IOutputCacheStore cache)
    {
        if (await db.Products.FindAsync(id) is Product product)
        {
            db.Products.Remove(product);
            await db.SaveChangesAsync();

            // Invalidate caches after delete
            await cache.EvictByTagAsync("lists", default);
            await cache.EvictByTagAsync("single", default);

            return Results.NoContent();
        }

        return Results.NotFound();
    }

    /// <summary>
    /// DELETE /bulk - Delete multiple products by IDs
    /// ✅ OPTIMIZED: Use HashSet for O(1) Contains() lookups instead of List O(n)
    /// </summary>
    private static async Task<IResult> DeleteProductsBulkHandler(
        [FromBody] List<int> productIds,
        [FromServices] AppDbContext db,
        [FromServices] IOutputCacheStore cache)
    {
        if (productIds == null || productIds.Count == 0)
            return Results.BadRequest("Product ID list cannot be empty");

        // ✅ OPTIMIZED: Convert to HashSet for O(1) Contains() in WHERE clause
        var productIdSet = productIds.ToHashSet();

        // ExecuteDeleteAsync: Zero memory usage, direct SQL DELETE
        // ✅ FAST: HashSet.Contains() is O(1) vs List.Contains() O(n)
        var deletedCount = await db.Products
            .Where(p => productIdSet.Contains(p.Id))
            .ExecuteDeleteAsync();

        if (deletedCount == 0)
            return Results.NotFound("No products found with the provided IDs");

        // Invalidate caches after bulk delete
        await cache.EvictByTagAsync("lists", default);
        await cache.EvictByTagAsync("single", default);

        return Results.Ok(new
        {
            Deleted = deletedCount,
            NotFound = productIds.Count - deletedCount,
            Message = $"Deleted {deletedCount} products, {productIds.Count - deletedCount} not found"
        });
    }
}
