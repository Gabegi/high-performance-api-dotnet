using ApexShop.Application.Features.Products.Handlers;
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
        group.MapPost("/", CreateProductHandlerWrapper)
            .WithName("CreateProduct")
            .WithDescription("Create a single product")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/bulk", CreateProductsBulkHandlerWrapper)
            .WithName("BulkCreateProducts")
            .WithDescription("Create multiple products in a single transaction using AddRange")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        return group;
    }

    private static async Task<IResult> CreateProductHandlerWrapper(
        Product product,
        AppDbContext db,
        IOutputCacheStore cache) =>
        await CreateProductHandler.Handle(product, db, cache);

    private static async Task<IResult> CreateProductsBulkHandlerWrapper(
        List<Product> products,
        AppDbContext db,
        IOutputCacheStore cache) =>
        await CreateProductsBulkHandler.Handle(products, db, cache);
}
