using ApexShop.API.DTOs;
using ApexShop.API.JsonContext;
using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Enums;
using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

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
                    o.Status.ToString(),
                    o.TotalAmount))
                .ToListAsync();

            var totalCount = await CompiledQueries.GetOrderCount(db); // ← Using compiled query

            return Results.Ok(new
            {
                Data = orders,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        });

        // Keyset (Cursor-based) Pagination - Optimized for deep pagination and large datasets
        group.MapGet("/cursor", async (AppDbContext db, int? afterId = null, int pageSize = 50) =>
        {
            // Validate pagination parameters
            pageSize = Math.Clamp(pageSize, 1, 100); // Max 100 items per page

            var query = db.Orders.AsNoTracking();

            // Apply cursor filter if provided
            if (afterId.HasValue)
            {
                query = query.Where(o => o.Id > afterId.Value);
            }

            // Fetch one extra to determine if there are more results
            var orders = await query
                .TagWith("GET /orders/cursor - Keyset pagination (optimized for deep pages)")
                .OrderBy(o => o.Id) // Required for consistent pagination and optimal index usage
                .Take(pageSize + 1)
                .Select(o => new OrderListDto(
                    o.Id,
                    o.UserId,
                    o.OrderDate,
                    o.Status.ToString(),
                    o.TotalAmount))
                .ToListAsync();

            var hasMore = orders.Count > pageSize;
            if (hasMore)
            {
                orders.RemoveAt(orders.Count - 1); // Remove the extra item
            }

            return Results.Ok(new
            {
                Data = orders,
                PageSize = pageSize,
                HasMore = hasMore,
                NextCursor = hasMore && orders.Count > 0 ? orders[^1].Id : (int?)null
            });
        }).WithName("GetOrdersCursor")
          .WithDescription("Keyset/cursor-based pagination - O(1) performance for any page depth. Use afterId parameter to continue from last record.");

        // Streaming - Get all orders with optional filters using IAsyncEnumerable
        group.MapGet("/stream", (AppDbContext db, int? userId = null, string? status = null, DateTime? fromDate = null, DateTime? toDate = null) =>
        {
            var query = db.Orders.AsNoTracking();

            // Apply optional filters
            if (userId.HasValue)
                query = query.Where(o => o.UserId == userId.Value);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
                query = query.Where(o => o.Status == orderStatus);

            if (fromDate.HasValue)
                query = query.Where(o => o.OrderDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(o => o.OrderDate <= toDate.Value);

            return query
                .TagWith("GET /orders/stream - Stream all orders with filters (constant memory)")
                .OrderBy(o => o.Id)
                .Select(o => new OrderListDto(
                    o.Id,
                    o.UserId,
                    o.OrderDate,
                    o.Status.ToString(),
                    o.TotalAmount))
                .AsAsyncEnumerable();
        }).WithName("StreamOrders")
          .WithDescription("Stream all orders using IAsyncEnumerable - constant memory regardless of result set size. Supports filters: userId, status, fromDate, toDate")
          .Produces<IAsyncEnumerable<OrderListDto>>(StatusCodes.Status200OK);

        // NDJSON Export
        group.MapGet("/export/ndjson", async (HttpContext context, AppDbContext db, int? userId = null, string? status = null, DateTime? fromDate = null, DateTime? toDate = null, int limit = 50000, CancellationToken cancellationToken = default) =>
        {
            try
            {
                limit = Math.Clamp(limit, 1, 50000);
                context.Response.ContentType = "application/x-ndjson";
                context.Response.Headers.Append("Content-Disposition", "attachment; filename=orders.ndjson");

                var query = db.Orders.AsNoTracking();
                if (userId.HasValue)
                    query = query.Where(o => o.UserId == userId.Value);
                if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
                    query = query.Where(o => o.Status == orderStatus);
                if (fromDate.HasValue)
                    query = query.Where(o => o.OrderDate >= fromDate.Value);
                if (toDate.HasValue)
                    query = query.Where(o => o.OrderDate <= toDate.Value);

                var filteredQuery = query
                    .TagWith("GET /orders/export/ndjson - NDJSON export")
                    .OrderBy(o => o.Id)
                    .Take(limit)
                    .Select(o => new OrderListDto(o.Id, o.UserId, o.OrderDate, o.Status.ToString(), o.TotalAmount));

                int exportedCount = 0;
                await foreach (var order in filteredQuery.AsAsyncEnumerable().WithCancellation(cancellationToken))
                {
                    await JsonSerializer.SerializeAsync(context.Response.Body, order, ApexShopJsonContext.Default.OrderListDto, cancellationToken);
                    await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("\n"), cancellationToken);
                    if (++exportedCount % 100 == 0)
                        await context.Response.Body.FlushAsync(cancellationToken);
                }
                await context.Response.Body.FlushAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                context.Response.HttpContext.Abort();
            }
            catch
            {
                context.Response.HttpContext.Abort();
                throw;
            }
        }).WithName("ExportOrdersNdjson")
          .WithDescription("Export orders as NDJSON. Supports filters: userId, status, fromDate, toDate. Max 50K items.")
          .Produces(StatusCodes.Status200OK);

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
        {
            var order = await CompiledQueries.GetOrderById(db, id);
            if (order is null) return Results.NotFound();

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
        });

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

        // ExecuteDelete - Bulk delete old orders
        group.MapDelete("/bulk-delete-old", async (int olderThanDays, AppDbContext db) =>
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
            var affectedRows = await db.Orders
                .Where(o => o.OrderDate < cutoffDate && o.Status == OrderStatus.Delivered)
                .ExecuteDeleteAsync();

            return Results.Ok(new { AffectedRows = affectedRows, Message = $"Deleted {affectedRows} orders older than {olderThanDays} days" });
        }).WithName("BulkDeleteOldOrders")
          .WithDescription("Bulk delete delivered orders older than specified days without loading entities into memory");
    }
}
