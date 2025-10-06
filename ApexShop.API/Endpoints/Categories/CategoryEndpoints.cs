using ApexShop.Domain.Entities;
using ApexShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.API.Endpoints.Categories;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/categories").WithTags("Categories");

        group.MapGet("/", async (AppDbContext db) =>
            await db.Categories.AsNoTracking().ToListAsync());

        group.MapGet("/{id}", async (int id, AppDbContext db) =>
            await db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id)
                is Category category ? Results.Ok(category) : Results.NotFound());

        group.MapPost("/", async (Category category, AppDbContext db) =>
        {
            category.CreatedDate = DateTime.UtcNow;
            db.Categories.Add(category);
            await db.SaveChangesAsync();
            return Results.Created($"/categories/{category.Id}", category);
        });

        group.MapPut("/{id}", async (int id, Category inputCategory, AppDbContext db) =>
        {
            var category = await db.Categories.FindAsync(id);
            if (category is null) return Results.NotFound();

            category.Name = inputCategory.Name;
            category.Description = inputCategory.Description;

            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapDelete("/{id}", async (int id, AppDbContext db) =>
        {
            if (await db.Categories.FindAsync(id) is Category category)
            {
                db.Categories.Remove(category);
                await db.SaveChangesAsync();
                return Results.NoContent();
            }
            return Results.NotFound();
        });
    }
}
