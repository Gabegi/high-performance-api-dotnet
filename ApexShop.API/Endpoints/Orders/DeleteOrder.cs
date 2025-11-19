using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Enums;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.API.Endpoints.Orders;

/// <summary>
/// DELETE endpoints for removing orders.
/// - DELETE /{id} - Delete a single order
/// - DELETE /bulk-delete-old - Bulk delete old orders by date and status
/// </summary>
public static class DeleteOrderEndpoint
{
    public static RouteGroupBuilder MapDeleteOrder(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id}", DeleteOrderHandler)
            .WithName("DeleteOrder")
            .WithDescription("Delete a single order")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/bulk-delete-old", DeleteOldOrdersHandler)
            .WithName("BulkDeleteOldOrders")
            .WithDescription("Bulk delete delivered orders older than specified days without loading entities into memory")
            .Produces(StatusCodes.Status200OK);

        return group;
    }

    /// <summary>
    /// DELETE /{id} - Delete a single order
    /// </summary>
    private static async Task<IResult> DeleteOrderHandler(
        int id,
        AppDbContext db,
        IOutputCacheStore cache)
    {
        if (await db.Orders.FindAsync(id) is Order order)
        {
            db.Orders.Remove(order);
            await db.SaveChangesAsync();

            // Invalidate caches after delete
            await cache.EvictByTagAsync("lists", default);
            await cache.EvictByTagAsync("single", default);

            return Results.NoContent();
        }
        return Results.NotFound();
    }

    /// <summary>
    /// DELETE /bulk-delete-old - Bulk delete delivered orders older than specified days
    /// ExecuteDeleteAsync: Zero memory usage, direct SQL DELETE
    /// </summary>
    private static async Task<IResult> DeleteOldOrdersHandler(
        int olderThanDays,
        AppDbContext db)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
        var affectedRows = await db.Orders
            .Where(o => o.OrderDate < cutoffDate && o.Status == OrderStatus.Delivered)
            .ExecuteDeleteAsync();

        return Results.Ok(new
        {
            AffectedRows = affectedRows,
            Message = $"Deleted {affectedRows} orders older than {olderThanDays} days"
        });
    }
}
