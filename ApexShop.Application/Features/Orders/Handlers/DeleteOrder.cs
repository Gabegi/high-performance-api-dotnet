using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.Application.Features.Orders.Handlers;

/// <summary>
/// Handler for DELETE /orders/{id} - Delete a single order
/// </summary>
public static class DeleteOrderHandler
{
    public static async Task<IResult> Handle(
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
}

/// <summary>
/// Handler for DELETE /orders/bulk-delete-old - Bulk delete orders older than X days
/// OPTIMIZED: Use ExecuteDeleteAsync for zero-memory bulk deletes
/// Directly executes SQL DELETE without loading entities into memory
/// </summary>
public static class DeleteOldOrdersHandler
{
    public static async Task<IResult> Handle(
        int daysOld,
        AppDbContext db,
        IOutputCacheStore cache)
    {
        if (daysOld < 1)
            return Results.BadRequest("daysOld must be greater than 0");

        var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);

        // ExecuteDeleteAsync: Zero memory usage, direct SQL DELETE
        // No entities loaded into memory - just pure SQL execution
        var deletedCount = await db.Orders
            .Where(o => o.OrderDate < cutoffDate)
            .ExecuteDeleteAsync();

        if (deletedCount == 0)
            return Results.NotFound($"No orders found older than {daysOld} days");

        // Invalidate caches after bulk delete
        await cache.EvictByTagAsync("lists", default);
        await cache.EvictByTagAsync("single", default);

        return Results.Ok(new
        {
            Deleted = deletedCount,
            Message = $"Deleted {deletedCount} orders older than {daysOld} days (cutoff: {cutoffDate:yyyy-MM-dd})"
        });
    }
}
