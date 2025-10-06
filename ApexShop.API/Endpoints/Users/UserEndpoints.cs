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

        group.MapGet("/", async (AppDbContext db) =>
            await db.Users
                .AsNoTracking()
                .Select(u => new UserListDto(
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.IsActive))
                .ToListAsync());

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
            await db.Users
                .AsNoTracking()
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
