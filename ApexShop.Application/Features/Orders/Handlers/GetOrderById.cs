using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using ApexShop.Application.DTOs;

namespace ApexShop.Application.Features.Orders.Handlers;

/// <summary>
/// Handler for GET /orders/{id} - Retrieve a single order by ID
/// Uses compiled queries for optimal performance.
/// </summary>
public static class GetOrderByIdHandler
{
    public static async Task<IResult> Handle(
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
