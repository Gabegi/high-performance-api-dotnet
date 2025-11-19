using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.OutputCaching;

namespace ApexShop.API.Endpoints.Products;

/// <summary>
/// POST endpoints for creating products.
/// - POST / - Create a single product
/// - POST /bulk - Create multiple products in a single transaction
/// </summary>
public static class CreateProductEndpoint
{
    public static RouteGroupBuilder MapCreateProduct(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateProductHandler)
            .WithName("CreateProduct")
            .WithDescription("Create a single product")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/bulk", CreateProductsBulkHandler)
            .WithName("BulkCreateProducts")
            .WithDescription("Create multiple products in a single transaction using AddRange")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        return group;
    }

    /// <summary>
    /// POST / - Create a single product
    /// </summary>
    private static async Task<IResult> CreateProductHandler(
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

    /// <summary>
    /// POST /bulk - Create multiple products
    /// Batch POST - Create multiple products in a single transaction
    /// </summary>
    private static async Task<IResult> CreateProductsBulkHandler(
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
