using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.OutputCaching;

namespace ApexShop.Application.Features.Products.Handlers;

/// <summary>
/// Handler for POST /products - Create a single product
/// </summary>
public static class CreateProductHandler
{
    public static async Task<IResult> Handle(
        Product product,
        AppDbContext db,
        IOutputCacheStore cache)
    {
        product.CreatedDate = DateTime.UtcNow;
        db.Products.Add(product);
        await db.SaveChangesAsync();

        // Invalidate list caches after creating new product
        await cache.EvictByTagAsync("lists", default);

        return Results.Created($"/products/{product.Id}", product);
    }
}
