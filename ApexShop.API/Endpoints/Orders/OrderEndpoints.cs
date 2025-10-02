using ApexShop.Domain.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.API.Endpoints.Orders;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders").WithTags("Orders");

        group.MapGet("/", async (AppDbContext db) =>
            await db.Orders.Include(o => o.User).Include(o => o.OrderItems).ToListAsync());

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
            await db.Orders.Include(o => o.User).Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id)
                is Order order ? Results.Ok(order) : Results.NotFound());

        group.MapPost("/", async (Order order, AppDbContext db) =>
        {
            order.OrderDate = DateTime.UtcNow;
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            return Results.Created($"/orders/{order.Id}", order);
        });

        group.MapPut("/{id}", async (int id, Order inputOrder, AppDbContext db) =>
        {
            var order = await db.Orders.FindAsync(id);
            if (order is null) return Results.NotFound();

            order.Status = inputOrder.Status;
            order.ShippingAddress = inputOrder.ShippingAddress;
            order.TrackingNumber = inputOrder.TrackingNumber;
            order.ShippedDate = inputOrder.ShippedDate;
            order.DeliveredDate = inputOrder.DeliveredDate;
            order.TotalAmount = inputOrder.TotalAmount;

            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapDelete("/{id}", async (int id, AppDbContext db) =>
        {
            if (await db.Orders.FindAsync(id) is Order order)
            {
                db.Orders.Remove(order);
                await db.SaveChangesAsync();
                return Results.NoContent();
            }
            return Results.NotFound();
        });
    }
}
