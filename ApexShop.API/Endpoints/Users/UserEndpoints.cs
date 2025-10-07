using ApexShop.API.DTOs;
using ApexShop.API.Queries;
using ApexShop.Domain.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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

            var totalCount = await db.Users.CountAsync();

            return Results.Ok(new
            {
                Data = users,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        });

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
            await CompiledQueries.GetUserById(db, id)
                is UserDto user ? Results.Ok(user) : Results.NotFound());

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

        // Batch PUT - Update multiple users
        group.MapPut("/bulk", async (List<User> users, AppDbContext db) =>
        {
            if (users == null || users.Count == 0)
                return Results.BadRequest("User list cannot be empty");

            var userIds = users.Select(u => u.Id).ToList();
            var existingUsers = await db.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

            var notFound = new List<int>();
            var updated = 0;

            foreach (var inputUser in users)
            {
                if (existingUsers.TryGetValue(inputUser.Id, out var existingUser))
                {
                    existingUser.Email = inputUser.Email;
                    existingUser.PasswordHash = inputUser.PasswordHash;
                    existingUser.FirstName = inputUser.FirstName;
                    existingUser.LastName = inputUser.LastName;
                    existingUser.PhoneNumber = inputUser.PhoneNumber;
                    existingUser.IsActive = inputUser.IsActive;
                    existingUser.LastLoginDate = inputUser.LastLoginDate;
                    updated++;
                }
                else
                {
                    notFound.Add(inputUser.Id);
                }
            }

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                Updated = updated,
                NotFound = notFound,
                Message = $"Updated {updated} users, {notFound.Count} not found"
            });
        }).WithName("BulkUpdateUsers")
          .WithDescription("Update multiple users in a single transaction");

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
        group.MapDelete("/bulk", async (List<int> userIds, AppDbContext db) =>
        {
            if (userIds == null || userIds.Count == 0)
                return Results.BadRequest("User ID list cannot be empty");

            var users = await db.Users
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();

            if (users.Count == 0)
                return Results.NotFound("No users found with the provided IDs");

            db.Users.RemoveRange(users);
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                Deleted = users.Count,
                NotFound = userIds.Count - users.Count,
                Message = $"Deleted {users.Count} users, {userIds.Count - users.Count} not found"
            });
        }).WithName("BulkDeleteUsers")
          .WithDescription("Delete multiple users by IDs in a single transaction using RemoveRange");

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
