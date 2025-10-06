using ApexShop.API.DTOs;
using ApexShop.API.Queries;
using ApexShop.Domain.Entities;
using ApexShop.Infrastructure.Data;
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

            var totalCount = await db.Products.CountAsync();

            return Results.Ok(new
            {
                Data = products,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        });

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
            await CompiledQueries.GetProductById(db, id)
                is ProductDto product ? Results.Ok(product) : Results.NotFound());

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
