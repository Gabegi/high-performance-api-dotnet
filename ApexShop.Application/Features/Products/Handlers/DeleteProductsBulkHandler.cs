using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.Application.Features.Products.Handlers;

/// <summary>
/// Handler for DELETE /products/bulk - Delete multiple products by IDs
/// ✅ OPTIMIZED: Use HashSet for O(1) Contains() lookups instead of List O(n)
/// ExecuteDeleteAsync: Zero memory usage, direct SQL DELETE
/// </summary>
public static class DeleteProductsBulkHandler
{
    public static async Task<IResult> Handle(
        List<int> productIds,
        AppDbContext db,
        IOutputCacheStore cache)
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
