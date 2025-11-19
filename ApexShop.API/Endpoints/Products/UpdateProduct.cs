using ApexShop.Application.Features.Products.Handlers;
using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.OutputCaching;

namespace ApexShop.API.Endpoints.Products;

/// <summary>
/// PUT and PATCH endpoints for updating products.
/// - PUT /{id} - Update a single product
/// - PUT /bulk - Update multiple products with streaming and batching
/// - PATCH /bulk-update-stock - Bulk update stock for products in a category
/// </summary>
public static class UpdateProductEndpoint
{
    public static RouteGroupBuilder MapUpdateProduct(this RouteGroupBuilder group)
    {
        group.MapPut("/{id}", UpdateProductHandlerWrapper)
            .WithName("UpdateProduct")
            .WithDescription("Update a single product")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/bulk", UpdateProductsBulkHandlerWrapper)
            .WithName("BulkUpdateProducts")
            .WithDescription("Update multiple products using streaming with batching (constant memory ~5-10MB)")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPatch("/bulk-update-stock", UpdateProductStockHandlerWrapper)
            .WithName("BulkUpdateStock")
            .WithDescription("Bulk update stock for all products in a category without loading entities into memory")
            .Produces(StatusCodes.Status200OK);

        return group;
    }

    private static async Task<IResult> UpdateProductHandlerWrapper(
        int id,
        Product inputProduct,
        AppDbContext db,
        IOutputCacheStore cache) =>
        await UpdateProductHandler.Handle(id, inputProduct, db, cache);

    private static async Task<IResult> UpdateProductsBulkHandlerWrapper(
        List<Product> products,
        AppDbContext db,
        ILogger<Program> logger,
        IOutputCacheStore cache) =>
        await UpdateProductsBulkHandler.Handle(products, db, logger, cache);

    private static async Task<IResult> UpdateProductStockHandlerWrapper(
        int categoryId,
        int stockAdjustment,
        AppDbContext db) =>
        await UpdateProductStockHandler.Handle(categoryId, stockAdjustment, db);
}
