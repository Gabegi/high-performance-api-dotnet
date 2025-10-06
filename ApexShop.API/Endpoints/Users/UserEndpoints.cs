using ApexShop.API.DTOs;
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
            await db.Users
                .AsNoTracking()
                .TagWith("GET /users/{id} - Get user by ID")
                .Where(u => u.Id == id)
                .Select(u => new UserDto(
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.PhoneNumber,
                    u.IsActive,
                    u.CreatedDate,
                    u.LastLoginDate))
                .FirstOrDefaultAsync()
                is UserDto user ? Results.Ok(user) : Results.NotFound());

        group.MapPost("/", async (User user, AppDbContext db) =>
        {
            user.CreatedDate = DateTime.UtcNow;
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return Results.Created($"/users/{user.Id}", user);
        });

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
    }
}
