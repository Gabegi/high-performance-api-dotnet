using ApexShop.API.DTOs;
using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using Microsoft.AspNetCore.OutputCaching;

namespace ApexShop.API.Endpoints.Products;

/// <summary>
/// GET /{id} endpoint for retrieving a single product by ID.
/// Uses compiled queries for optimal performance.
/// </summary>
public static class GetProductByIdEndpoint
{
    public static RouteGroupBuilder MapGetProductById(this RouteGroupBuilder group)
    {
        group.MapGet("/{id}", GetProductByIdHandler)
            .CacheOutput("Single")
            .WithName("GetProductById")
            .WithDescription("Get a single product by ID");

        return group;
    }

    /// <summary>
    /// GET /{id} - Retrieve a single product by ID
    /// </summary>
    private static async Task<IResult> GetProductByIdHandler(
        int id,
        AppDbContext db)
    {
        var product = await CompiledQueries.GetProductById(db, id);
        if (product is null)
            return Results.NotFound();

        return Results.Ok(new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.Stock,
            product.CategoryId,
            product.CreatedDate,
            product.UpdatedDate));
    }
}
