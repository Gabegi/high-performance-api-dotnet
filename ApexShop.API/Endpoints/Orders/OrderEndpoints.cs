using ApexShop.API.DTOs;
using ApexShop.API.Queries;
using ApexShop.Domain.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.API.Endpoints.Orders;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders").WithTags("Orders");

        group.MapGet("/", async (AppDbContext db, int page = 1, int pageSize = 50) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var orders = await db.Orders
                .AsNoTracking()
                .TagWith("GET /orders - List orders with pagination")
                .OrderByDescending(o => o.OrderDate) // Most recent orders first
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OrderListDto(
                    o.Id,
                    o.UserId,
                    o.OrderDate,
                    o.Status,
                    o.TotalAmount))
                .ToListAsync();

            var totalCount = await db.Orders.CountAsync();

            return Results.Ok(new
            {
                Data = orders,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        });

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
            await CompiledQueries.GetOrderById(db, id)
                is OrderDto order ? Results.Ok(order) : Results.NotFound());

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
