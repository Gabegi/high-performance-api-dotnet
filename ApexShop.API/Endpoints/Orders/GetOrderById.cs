using ApexShop.API.DTOs;
using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using Microsoft.AspNetCore.OutputCaching;

namespace ApexShop.API.Endpoints.Orders;

/// <summary>
/// GET /{id} endpoint for retrieving a single order by ID.
/// Uses compiled queries for optimal performance.
/// </summary>
public static class GetOrderByIdEndpoint
{
    public static RouteGroupBuilder MapGetOrderById(this RouteGroupBuilder group)
    {
        group.MapGet("/{id}", GetOrderByIdHandler)
            .CacheOutput("Single")
            .WithName("GetOrderById")
            .WithDescription("Get a single order by ID");

        return group;
    }

    /// <summary>
    /// GET /{id} - Retrieve a single order by ID
    /// </summary>
    private static async Task<IResult> GetOrderByIdHandler(
        int id,
        AppDbContext db)
    {
        var order = await CompiledQueries.GetOrderById(db, id);
        if (order is null)
            return Results.NotFound();

        return Results.Ok(new OrderDto(
            order.Id,
            order.UserId,
            order.OrderDate,
            order.Status.ToString(),
            order.TotalAmount,
            order.ShippingAddress,
            order.TrackingNumber,
            order.ShippedDate,
            order.DeliveredDate));
    }
}
