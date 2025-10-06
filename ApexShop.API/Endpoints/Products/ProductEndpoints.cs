using ApexShop.Domain.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.API.Endpoints.Products;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products").WithTags("Products");

        group.MapGet("/", async (AppDbContext db) =>
            await db.Products.AsNoTracking().ToListAsync());

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
            await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id)
                is Product product ? Results.Ok(product) : Results.NotFound());

        group.MapPost("/", async (Product product, AppDbContext db) =>
        {
            product.CreatedDate = DateTime.UtcNow;
            db.Products.Add(product);
            await db.SaveChangesAsync();
            return Results.Created($"/products/{product.Id}", product);
        });

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
    }
}
