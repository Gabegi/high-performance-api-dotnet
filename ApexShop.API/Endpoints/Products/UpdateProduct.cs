using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.API.Endpoints.Products;

/// <summary>
/// PUT and PATCH endpoints for updating products.
/// - PUT /{id} - Update a single product
/// - PUT /bulk - Update multiple products with streaming and batching
/// - PATCH /bulk-update-stock - Bulk update stock for products in a category
/// </summary>
public static class UpdateProductEndpoint
{
    public static RouteGroupBuilder MapUpdateProduct(this RouteGroupBuilder group)
    {
        group.MapPut("/{id}", UpdateProductHandler)
            .WithName("UpdateProduct")
            .WithDescription("Update a single product")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/bulk", UpdateProductsBulkHandler)
            .WithName("BulkUpdateProducts")
            .WithDescription("Update multiple products using streaming with batching (constant memory ~5-10MB)")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPatch("/bulk-update-stock", UpdateProductStockHandler)
            .WithName("BulkUpdateStock")
            .WithDescription("Bulk update stock for all products in a category without loading entities into memory")
            .Produces(StatusCodes.Status200OK);

        return group;
    }

    /// <summary>
    /// PUT /{id} - Update a single product
    /// </summary>
    private static async Task<IResult> UpdateProductHandler(
        int id,
        Product inputProduct,
        AppDbContext db,
        IOutputCacheStore cache)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null)
            return Results.NotFound();

        product.Name = inputProduct.Name;
        product.Description = inputProduct.Description;
        product.Price = inputProduct.Price;
        product.Stock = inputProduct.Stock;
        product.CategoryId = inputProduct.CategoryId;
        product.UpdatedDate = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // Invalidate both list and single item caches after update
        await cache.EvictByTagAsync("lists", default);
        await cache.EvictByTagAsync("single", default);

        return Results.NoContent();
    }

    /// <summary>
    /// PUT /bulk - Update multiple products with streaming
    /// ✅ OPTIMIZED: Use HashSet for O(1) Contains() lookups instead of List O(n)
    /// </summary>
    private static async Task<IResult> UpdateProductsBulkHandler(
        List<Product> products,
        AppDbContext db,
        ILogger<Program> logger,
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

    /// <summary>
    /// PATCH /bulk-update-stock - ExecuteUpdate - Bulk update stock for products in a category
    /// </summary>
    private static async Task<IResult> UpdateProductStockHandler(
        int categoryId,
        int stockAdjustment,
        AppDbContext db)
    {
        var affectedRows = await db.Products
            .Where(p => p.CategoryId == categoryId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Stock, p => p.Stock + stockAdjustment)
                .SetProperty(p => p.UpdatedDate, DateTime.UtcNow));

        return Results.Ok(new
        {
            AffectedRows = affectedRows,
            Message = $"Updated stock for {affectedRows} products in category {categoryId}"
        });
    }
}
