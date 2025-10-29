using ApexShop.API.DTOs;
using ApexShop.API.JsonContext;
using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace ApexShop.API.Endpoints.Users;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users").WithTags("Users");

        group.MapGet("/", async (AppDbContext db, int page = 1, int pageSize = 50) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var users = await db.Users
                .AsNoTracking()
                .TagWith("GET /users - List users with pagination")
                .OrderBy(u => u.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserListDto(
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.IsActive))
                .ToListAsync();

            var totalCount = await CompiledQueries.GetUserCount(db); // ← Using compiled query

            return Results.Ok(new
            {
                Data = users,
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

            var query = db.Users.AsNoTracking();

            // Apply cursor filter if provided
            if (afterId.HasValue)
            {
                query = query.Where(u => u.Id > afterId.Value);
            }

            // Fetch one extra to determine if there are more results
            var users = await query
                .TagWith("GET /users/cursor - Keyset pagination (optimized for deep pages)")
                .OrderBy(u => u.Id) // Required for consistent pagination and optimal index usage
                .Take(pageSize + 1)
                .Select(u => new UserListDto(
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.IsActive))
                .ToListAsync();

            var hasMore = users.Count > pageSize;
            if (hasMore)
            {
                users.RemoveAt(users.Count - 1); // Remove the extra item
            }

            return Results.Ok(new
            {
                Data = users,
                PageSize = pageSize,
                HasMore = hasMore,
                NextCursor = hasMore && users.Count > 0 ? users[^1].Id : (int?)null
            });
        }).WithName("GetUsersCursor")
          .WithDescription("Keyset/cursor-based pagination - O(1) performance for any page depth. Use afterId parameter to continue from last record.");

        // Streaming - Get all users with optional filters using IAsyncEnumerable
        group.MapGet("/stream", (AppDbContext db, bool? isActive = null, DateTime? createdAfter = null) =>
        {
            var query = db.Users.AsNoTracking();

            // Apply optional filters
            if (isActive.HasValue)
                query = query.Where(u => u.IsActive == isActive.Value);

            if (createdAfter.HasValue)
                query = query.Where(u => u.CreatedDate >= createdAfter.Value);

            return query
                .TagWith("GET /users/stream - Stream all users with filters (constant memory)")
                .OrderBy(u => u.Id)
                .Select(u => new UserListDto(
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.IsActive))
                .AsAsyncEnumerable();
        }).WithName("StreamUsers")
          .WithDescription("Stream all users using IAsyncEnumerable - constant memory regardless of result set size. Supports filters: isActive, createdAfter")
          .Produces<IAsyncEnumerable<UserListDto>>(StatusCodes.Status200OK);

        // NDJSON Export
        group.MapGet("/export/ndjson", async (HttpContext context, AppDbContext db, bool? isActive = null, DateTime? createdAfter = null, int limit = 50000, CancellationToken cancellationToken = default) =>
        {
            try
            {
                limit = Math.Clamp(limit, 1, 50000);
                context.Response.ContentType = "application/x-ndjson";
                context.Response.Headers.Append("Content-Disposition", "attachment; filename=users.ndjson");

                var query = db.Users.AsNoTracking();
                if (isActive.HasValue)
                    query = query.Where(u => u.IsActive == isActive.Value);
                if (createdAfter.HasValue)
                    query = query.Where(u => u.CreatedDate >= createdAfter.Value);

                var filteredQuery = query
                    .TagWith("GET /users/export/ndjson - NDJSON export")
                    .OrderBy(u => u.Id)
                    .Take(limit)
                    .Select(u => new UserListDto(u.Id, u.Email, u.FirstName, u.LastName, u.IsActive));

                int exportedCount = 0;
                await foreach (var user in filteredQuery.AsAsyncEnumerable().WithCancellation(cancellationToken))
                {
                    await JsonSerializer.SerializeAsync(context.Response.Body, user, ApexShopJsonContext.Default.UserListDto, cancellationToken);
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
        }).WithName("ExportUsersNdjson")
          .WithDescription("Export users as NDJSON. Supports filters: isActive, createdAfter. Max 50K items.")
          .Produces(StatusCodes.Status200OK);

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
        {
            var user = await CompiledQueries.GetUserById(db, id);
            if (user is null) return Results.NotFound();

            return Results.Ok(new UserDto(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.PhoneNumber,
                user.IsActive,
                user.CreatedDate,
                user.LastLoginDate));
        });

        group.MapPost("/", async (User user, AppDbContext db) =>
        {
            user.CreatedDate = DateTime.UtcNow;
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return Results.Created($"/users/{user.Id}", user);
        });

        // Batch POST - Create multiple users
        group.MapPost("/bulk", async (List<User> users, AppDbContext db) =>
        {
            if (users == null || users.Count == 0)
                return Results.BadRequest("User list cannot be empty");

            var now = DateTime.UtcNow;
            foreach (var user in users)
            {
                user.CreatedDate = now;
            }

            db.Users.AddRange(users);
            await db.SaveChangesAsync();

            return Results.Created("/users/bulk", new
            {
                Count = users.Count,
                Message = $"Created {users.Count} users",
                UserIds = users.Select(u => u.Id).ToList()
            });
        }).WithName("BulkCreateUsers")
          .WithDescription("Create multiple users in a single transaction using AddRange");

        group.MapPut("/{id}", async (int id, User inputUser, AppDbContext db) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();

            user.Email = inputUser.Email;
            user.PasswordHash = inputUser.PasswordHash;
            user.FirstName = inputUser.FirstName;
            user.LastName = inputUser.LastName;
            user.PhoneNumber = inputUser.PhoneNumber;
            user.IsActive = inputUser.IsActive;
            user.LastLoginDate = inputUser.LastLoginDate;

            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Batch PUT - Update multiple users with streaming
        group.MapPut("/bulk", async (List<User> users, AppDbContext db, ILogger<Program> logger) =>
        {
            if (users == null || users.Count == 0)
                return Results.BadRequest("User list cannot be empty");

            // Create lookup dictionary for O(1) access
            var updateLookup = users.ToDictionary(u => u.Id);
            var userIds = updateLookup.Keys.ToList();

            using var transaction = await db.Database.BeginTransactionAsync();
            try
            {
                const int batchSize = 500;
                var batch = new List<User>(batchSize);
                var updated = 0;

                // Stream entities instead of loading all at once
                await foreach (var existingUser in db.Users
                    .AsTracking()
                    .Where(u => userIds.Contains(u.Id))
                    .AsAsyncEnumerable())
                {
                    // Apply per-entity updates
                    var inputUser = updateLookup[existingUser.Id];
                    existingUser.Email = inputUser.Email;
                    existingUser.PasswordHash = inputUser.PasswordHash;
                    existingUser.FirstName = inputUser.FirstName;
                    existingUser.LastName = inputUser.LastName;
                    existingUser.PhoneNumber = inputUser.PhoneNumber;
                    existingUser.IsActive = inputUser.IsActive;
                    existingUser.LastLoginDate = inputUser.LastLoginDate;

                    batch.Add(existingUser);
                    updateLookup.Remove(existingUser.Id); // Track processed items

                    // Save and clear batch
                    if (batch.Count >= batchSize)
                    {
                        await db.SaveChangesAsync();
                        db.ChangeTracker.Clear(); // Critical: Free memory
                        updated += batch.Count;
                        batch.Clear();

                        logger.LogInformation("Processed batch: {Count}/{Total} users", updated, users.Count);
                    }
                }

                // Process remaining items
                if (batch.Count > 0)
                {
                    await db.SaveChangesAsync();
                    updated += batch.Count;
                }

                await transaction.CommitAsync();

                // Remaining items in updateLookup were not found
                var notFound = updateLookup.Keys.ToList();

                return Results.Ok(new
                {
                    Updated = updated,
                    NotFound = notFound,
                    Message = $"Updated {updated} users, {notFound.Count} not found"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                logger.LogError(ex, "Bulk update failed, rolled back");
                return Results.Problem("Bulk update failed: " + ex.Message);
            }
        }).WithName("BulkUpdateUsers")
          .WithDescription("Update multiple users using streaming with batching (constant memory ~5-10MB)");

        group.MapDelete("/{id}", async (int id, AppDbContext db) =>
        {
            if (await db.Users.FindAsync(id) is User user)
            {
                db.Users.Remove(user);
                await db.SaveChangesAsync();
                return Results.NoContent();
            }
            return Results.NotFound();
        });

        // Batch DELETE - Delete multiple users by IDs
        group.MapDelete("/bulk", async ([FromBody] List<int> userIds, [FromServices] AppDbContext db) =>
        {
            if (userIds == null || userIds.Count == 0)
                return Results.BadRequest("User ID list cannot be empty");

            // ExecuteDeleteAsync: Zero memory usage, direct SQL DELETE
            var deletedCount = await db.Users
                .Where(u => userIds.Contains(u.Id))
                .ExecuteDeleteAsync();

            if (deletedCount == 0)
                return Results.NotFound("No users found with the provided IDs");

            return Results.Ok(new
            {
                Deleted = deletedCount,
                NotFound = userIds.Count - deletedCount,
                Message = $"Deleted {deletedCount} users, {userIds.Count - deletedCount} not found"
            });
        }).WithName("BulkDeleteUsers")
          .WithDescription("Delete multiple users by IDs without loading entities into memory (ExecuteDeleteAsync)");

        // ExecuteUpdate - Deactivate inactive users
        group.MapPatch("/bulk-deactivate-inactive", async (int inactiveDays, AppDbContext db) =>
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-inactiveDays);
            var affectedRows = await db.Users
                .Where(u => u.LastLoginDate < cutoffDate && u.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));

            return Results.Ok(new { AffectedRows = affectedRows, Message = $"Deactivated {affectedRows} users inactive for {inactiveDays}+ days" });
        }).WithName("BulkDeactivateInactiveUsers")
          .WithDescription("Bulk deactivate users who haven't logged in for specified days without loading entities into memory");
    }
}
