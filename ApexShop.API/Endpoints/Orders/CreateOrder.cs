using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.OutputCaching;

namespace ApexShop.API.Endpoints.Orders;

/// <summary>
/// POST endpoints for creating orders.
/// - POST / - Create a single order
/// </summary>
public static class CreateOrderEndpoint
{
    public static RouteGroupBuilder MapCreateOrder(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateOrderHandler)
            .WithName("CreateOrder")
            .WithDescription("Create a single order")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        return group;
    }

    /// <summary>
    /// POST / - Create a single order
    /// </summary>
    private static async Task<IResult> CreateOrderHandler(
        Order order,
        AppDbContext db,
        IOutputCacheStore cache)
    {
        order.OrderDate = DateTime.UtcNow;
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // Invalidate caches after creating new order
        await cache.EvictByTagAsync("lists", default);

        return Results.Created($"/orders/{order.Id}", order);
    }
}
