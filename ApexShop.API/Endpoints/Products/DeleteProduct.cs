using ApexShop.Application.Features.Products.Handlers;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ApexShop.API.Endpoints.Products;

/// <summary>
/// DELETE endpoints for removing products.
/// - DELETE /{id} - Delete a single product
/// - DELETE /bulk - Delete multiple products by IDs
/// </summary>
public static class DeleteProductEndpoint
{
    public static RouteGroupBuilder MapDeleteProduct(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id}", DeleteProductHandlerWrapper)
            .WithName("DeleteProduct")
            .WithDescription("Delete a single product")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/bulk", DeleteProductsBulkHandlerWrapper)
            .WithName("BulkDeleteProducts")
            .WithDescription("Delete multiple products by IDs without loading entities into memory (ExecuteDeleteAsync)")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> DeleteProductHandlerWrapper(
        int id,
        AppDbContext db,
        IOutputCacheStore cache) =>
        await DeleteProductHandler.Handle(id, db, cache);

    private static async Task<IResult> DeleteProductsBulkHandlerWrapper(
        [FromBody] List<int> productIds,
        [FromServices] AppDbContext db,
        [FromServices] IOutputCacheStore cache) =>
        await DeleteProductsBulkHandler.Handle(productIds, db, cache);
}
