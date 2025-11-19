using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.OutputCaching;

namespace ApexShop.Application.Features.Orders.Handlers;

/// <summary>
/// Handler for POST /orders - Create a single order
/// </summary>
public static class CreateOrderHandler
{
    public static async Task<IResult> Handle(
        Order order,
        AppDbContext db,
        IOutputCacheStore cache)
    {
        order.OrderDate = DateTime.UtcNow;
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // Invalidate list caches after creating new order
        await cache.EvictByTagAsync("lists", default);

        return Results.Created($"/orders/{order.Id}", order);
    }
}
