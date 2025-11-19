using ApexShop.Application.Features.Products.Handlers;
using ApexShop.Infrastructure.Data;
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
        group.MapGet("/{id}", GetProductByIdHandlerWrapper)
            .CacheOutput("Single")
            .WithName("GetProductById")
            .WithDescription("Get a single product by ID");

        return group;
    }

    private static async Task<IResult> GetProductByIdHandlerWrapper(
        int id,
        AppDbContext db) =>
        await GetProductByIdHandler.Handle(id, db);
}
