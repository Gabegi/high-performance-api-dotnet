using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.OutputCaching;

namespace ApexShop.API.Endpoints.Orders;

/// <summary>
/// PUT endpoints for updating orders.
/// - PUT /{id} - Update a single order
/// </summary>
public static class UpdateOrderEndpoint
{
    public static RouteGroupBuilder MapUpdateOrder(this RouteGroupBuilder group)
    {
        group.MapPut("/{id}", UpdateOrderHandler)
            .WithName("UpdateOrder")
            .WithDescription("Update a single order")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    /// <summary>
    /// PUT /{id} - Update a single order
    /// </summary>
    private static async Task<IResult> UpdateOrderHandler(
        int id,
        Order inputOrder,
        AppDbContext db,
        IOutputCacheStore cache)
    {
        var order = await db.Orders.FindAsync(id);
        if (order is null)
            return Results.NotFound();

        order.Status = inputOrder.Status;
        order.ShippingAddress = inputOrder.ShippingAddress;
        order.TrackingNumber = inputOrder.TrackingNumber;
        order.ShippedDate = inputOrder.ShippedDate;
        order.DeliveredDate = inputOrder.DeliveredDate;
        order.TotalAmount = inputOrder.TotalAmount;

        await db.SaveChangesAsync();

        // Invalidate caches after update
        await cache.EvictByTagAsync("lists", default);
        await cache.EvictByTagAsync("single", default);

        return Results.NoContent();
    }
}
