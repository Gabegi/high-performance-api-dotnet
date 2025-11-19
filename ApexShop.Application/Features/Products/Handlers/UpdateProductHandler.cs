using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.OutputCaching;

namespace ApexShop.Application.Features.Products.Handlers;

/// <summary>
/// Handler for PUT /products/{id} - Update a single product
/// </summary>
public static class UpdateProductHandler
{
    public static async Task<IResult> Handle(
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
}
