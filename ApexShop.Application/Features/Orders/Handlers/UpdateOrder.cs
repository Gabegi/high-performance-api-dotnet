using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.OutputCaching;

namespace ApexShop.Application.Features.Orders.Handlers;

/// <summary>
/// Handler for PUT /orders/{id} - Update a single order
/// </summary>
public static class UpdateOrderHandler
{
    public static async Task<IResult> Handle(
        int id,
        Order inputOrder,
        AppDbContext db,
        IOutputCacheStore cache)
    {
        var order = await db.Orders.FindAsync(id);
        if (order is null)
            return Results.NotFound();

        order.UserId = inputOrder.UserId;
        order.TotalAmount = inputOrder.TotalAmount;
        order.Status = inputOrder.Status;
        order.ShippingAddress = inputOrder.ShippingAddress;
        order.TrackingNumber = inputOrder.TrackingNumber;
        order.ShippedDate = inputOrder.ShippedDate;
        order.DeliveredDate = inputOrder.DeliveredDate;

        await db.SaveChangesAsync();

        // Invalidate both list and single item caches after update
        await cache.EvictByTagAsync("lists", default);
        await cache.EvictByTagAsync("single", default);

        return Results.NoContent();
    }
}
