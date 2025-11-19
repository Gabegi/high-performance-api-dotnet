using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.Application.Features.Products.Handlers;

/// <summary>
/// Handler for PUT /products/bulk - Update multiple products with streaming and batching
/// ✅ OPTIMIZED: Use HashSet for O(1) Contains() lookups instead of List O(n)
/// </summary>
public static class UpdateProductsBulkHandler
{
    public static async Task<IResult> Handle(
        List<Product> products,
        AppDbContext db,
        ILogger logger,
        IOutputCacheStore cache)
    {
        if (products == null || products.Count == 0)
            return Results.BadRequest("Product list cannot be empty");

        // Create lookup dictionary for O(1) access (input data - unavoidable memory usage)
        var updateLookup = products.ToDictionary(p => p.Id);
        // ✅ OPTIMIZED: Use HashSet instead of List for O(1) Contains() per row
        var productIds = updateLookup.Keys.ToHashSet();

        using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            const int batchSize = 500;
            var batch = new List<Product>(batchSize);
            var updated = 0;
            var now = DateTime.UtcNow;

            // Stream entities instead of loading all at once
            // ✅ FAST: HashSet.Contains() is O(1) vs List.Contains() O(n)
            await foreach (var existingProduct in db.Products
                .AsTracking()
                .Where(p => productIds.Contains(p.Id))
                .AsAsyncEnumerable())
            {
                // Apply per-entity updates
                var inputProduct = updateLookup[existingProduct.Id];
                existingProduct.Name = inputProduct.Name;
                existingProduct.Description = inputProduct.Description;
                existingProduct.Price = inputProduct.Price;
                existingProduct.Stock = inputProduct.Stock;
                existingProduct.CategoryId = inputProduct.CategoryId;
                existingProduct.UpdatedDate = now;

                batch.Add(existingProduct);
                updateLookup.Remove(existingProduct.Id); // Track processed items

                // Save and clear batch
                if (batch.Count >= batchSize)
                {
                    await db.SaveChangesAsync();
                    db.ChangeTracker.Clear(); // Critical: Free memory
                    updated += batch.Count;
                    batch.Clear();

                    logger.LogInformation("Processed batch: {Count}/{Total} products", updated, products.Count);
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

            // Invalidate caches after bulk update
            await cache.EvictByTagAsync("lists", default);
            await cache.EvictByTagAsync("single", default);

            return Results.Ok(new
            {
                Updated = updated,
                NotFound = notFound,
                Message = $"Updated {updated} products, {notFound.Count} not found"
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
