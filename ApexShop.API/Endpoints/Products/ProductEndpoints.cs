using ApexShop.API.DTOs;
using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.API.Endpoints.Products;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products").WithTags("Products");

        group.MapGet("/", async (AppDbContext db, int page = 1, int pageSize = 50) =>
        {
            // Validate pagination parameters
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100); // Max 100 items per page

            var products = await db.Products
                .AsNoTracking()
                .TagWith("GET /products - List products with pagination")
                .OrderBy(p => p.Id) // Required for consistent pagination
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductListDto(
                    p.Id,
                    p.Name,
                    p.Price,
                    p.Stock,
                    p.CategoryId))
                .ToListAsync();

            var totalCount = await CompiledQueries.GetProductCount(db); // ← Using compiled query

            return Results.Ok(new
            {
                Data = products,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        });

        // Keyset (Cursor-based) Pagination - Optimized for deep pagination and large datasets
        group.MapGet("/cursor", async (AppDbContext db, int? afterId = null, int pageSize = 50) =>
        {
            // Validate pagination parameters
            pageSize = Math.Clamp(pageSize, 1, 100); // Max 100 items per page

            var query = db.Products.AsNoTracking();

            // Apply cursor filter if provided
            if (afterId.HasValue)
            {
                query = query.Where(p => p.Id > afterId.Value);
            }

            // Fetch one extra to determine if there are more results
            var products = await query
                .TagWith("GET /products/cursor - Keyset pagination (optimized for deep pages)")
                .OrderBy(p => p.Id) // Required for consistent pagination
                .Take(pageSize + 1)
                .Select(p => new ProductListDto(
                    p.Id,
                    p.Name,
                    p.Price,
                    p.Stock,
                    p.CategoryId))
                .ToListAsync();

            var hasMore = products.Count > pageSize;
            if (hasMore)
            {
                products.RemoveAt(products.Count - 1); // Remove the extra item
            }

            return Results.Ok(new
            {
                Data = products,
                PageSize = pageSize,
                HasMore = hasMore,
                NextCursor = hasMore && products.Count > 0 ? products[^1].Id : (int?)null
            });
        }).WithName("GetProductsCursor")
          .WithDescription("Keyset/cursor-based pagination - O(1) performance for any page depth. Use afterId parameter to continue from last record.");

        // Streaming - Get all products with optional filters using IAsyncEnumerable
        group.MapGet("/stream", (AppDbContext db, int? categoryId = null, decimal? minPrice = null, decimal? maxPrice = null, bool? inStock = null) =>
        {
            var query = db.Products.AsNoTracking();

            // Apply optional filters
            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            if (minPrice.HasValue)
                query = query.Where(p => p.Price >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(p => p.Price <= maxPrice.Value);

            if (inStock.HasValue && inStock.Value)
                query = query.Where(p => p.Stock > 0);

            return query
                .TagWith("GET /products/stream - Stream all products with filters (constant memory)")
                .OrderBy(p => p.Id)
                .Select(p => new ProductListDto(
                    p.Id,
                    p.Name,
                    p.Price,
                    p.Stock,
                    p.CategoryId))
                .AsAsyncEnumerable();
        }).WithName("StreamProducts")
          .WithDescription("Stream all products using IAsyncEnumerable - constant memory regardless of result set size. Supports filters: categoryId, minPrice, maxPrice, inStock")
          .Produces<IAsyncEnumerable<ProductListDto>>(StatusCodes.Status200OK);

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
        {
            var product = await CompiledQueries.GetProductById(db, id);
            if (product is null) return Results.NotFound();

            return Results.Ok(new ProductDto(
                product.Id,
                product.Name,
                product.Description,
                product.Price,
                product.Stock,
                product.CategoryId,
                product.CreatedDate,
                product.UpdatedDate));
        });

        group.MapPost("/", async (Product product, AppDbContext db) =>
        {
            product.CreatedDate = DateTime.UtcNow;
            db.Products.Add(product);
            await db.SaveChangesAsync();
            return Results.Created($"/products/{product.Id}", product);
        });

        // Batch POST - Create multiple products
        group.MapPost("/bulk", async (List<Product> products, AppDbContext db) =>
        {
            if (products == null || products.Count == 0)
                return Results.BadRequest("Product list cannot be empty");

            var now = DateTime.UtcNow;
            foreach (var product in products)
            {
                product.CreatedDate = now;
            }

            db.Products.AddRange(products);
            await db.SaveChangesAsync();

            return Results.Created("/products/bulk", new
            {
                Count = products.Count,
                Message = $"Created {products.Count} products",
                ProductIds = products.Select(p => p.Id).ToList()
            });
        }).WithName("BulkCreateProducts")
          .WithDescription("Create multiple products in a single transaction using AddRange");


        group.MapPut("/{id}", async (int id, Product inputProduct, AppDbContext db) =>
        {
            var product = await db.Products.FindAsync(id);
            if (product is null) return Results.NotFound();

            product.Name = inputProduct.Name;
            product.Description = inputProduct.Description;
            product.Price = inputProduct.Price;
            product.Stock = inputProduct.Stock;
            product.CategoryId = inputProduct.CategoryId;
            product.UpdatedDate = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Batch PUT - Update multiple products with streaming
        group.MapPut("/bulk", async (List<Product> products, AppDbContext db, ILogger<Program> logger) =>
        {
            if (products == null || products.Count == 0)
                return Results.BadRequest("Product list cannot be empty");

            // Create lookup dictionary for O(1) access (input data - unavoidable memory usage)
            var updateLookup = products.ToDictionary(p => p.Id);
            var productIds = updateLookup.Keys.ToList();

            using var transaction = await db.Database.BeginTransactionAsync();
            try
            {
                const int batchSize = 500;
                var batch = new List<Product>(batchSize);
                var updated = 0;
                var now = DateTime.UtcNow;

                // Stream entities instead of loading all at once
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
        }).WithName("BulkUpdateProducts")
          .WithDescription("Update multiple products using streaming with batching (constant memory ~5-10MB)");


        group.MapDelete("/{id}", async (int id, AppDbContext db) =>
        {
            if (await db.Products.FindAsync(id) is Product product)
            {
                db.Products.Remove(product);
                await db.SaveChangesAsync();
                return Results.NoContent();
            }
            return Results.NotFound();
        });

        // Batch DELETE - Delete multiple products by IDs
        group.MapDelete("/bulk", async (List<int> productIds, AppDbContext db) =>
        {
            if (productIds == null || productIds.Count == 0)
                return Results.BadRequest("Product ID list cannot be empty");

            // ExecuteDeleteAsync: Zero memory usage, direct SQL DELETE
            var deletedCount = await db.Products
                .Where(p => productIds.Contains(p.Id))
                .ExecuteDeleteAsync();

            if (deletedCount == 0)
                return Results.NotFound("No products found with the provided IDs");

            return Results.Ok(new
            {
                Deleted = deletedCount,
                NotFound = productIds.Count - deletedCount,
                Message = $"Deleted {deletedCount} products, {productIds.Count - deletedCount} not found"
            });
        }).WithName("BulkDeleteProducts")
          .WithDescription("Delete multiple products by IDs without loading entities into memory (ExecuteDeleteAsync)");


        // ExecuteUpdate - Bulk update stock for products in a category
        group.MapPatch("/bulk-update-stock", async (int categoryId, int stockAdjustment, AppDbContext db) =>
        {
            var affectedRows = await db.Products
                .Where(p => p.CategoryId == categoryId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Stock, p => p.Stock + stockAdjustment)
                    .SetProperty(p => p.UpdatedDate, DateTime.UtcNow));

            return Results.Ok(new { AffectedRows = affectedRows, Message = $"Updated stock for {affectedRows} products in category {categoryId}" });
        }).WithName("BulkUpdateStock")
          .WithDescription("Bulk update stock for all products in a category without loading entities into memory");
    }
}
