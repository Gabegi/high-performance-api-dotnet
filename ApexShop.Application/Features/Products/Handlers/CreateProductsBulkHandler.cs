using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.OutputCaching;

namespace ApexShop.Application.Features.Products.Handlers;

/// <summary>
/// Handler for POST /products/bulk - Create multiple products in a single transaction
/// </summary>
public static class CreateProductsBulkHandler
{
    public static async Task<IResult> Handle(
        List<Product> products,
        AppDbContext db,
        IOutputCacheStore cache)
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

        // Invalidate list caches after bulk create
        await cache.EvictByTagAsync("lists", default);

        return Results.Created("/products/bulk", new
        {
            Count = products.Count,
            Message = $"Created {products.Count} products",
            ProductIds = products.Select(p => p.Id).ToList()
        });
    }
}
