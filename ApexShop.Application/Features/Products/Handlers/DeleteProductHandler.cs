using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.OutputCaching;

namespace ApexShop.Application.Features.Products.Handlers;

/// <summary>
/// Handler for DELETE /products/{id} - Delete a single product
/// </summary>
public static class DeleteProductHandler
{
    public static async Task<IResult> Handle(
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
}
