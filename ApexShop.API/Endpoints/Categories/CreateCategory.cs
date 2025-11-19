using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;

namespace ApexShop.API.Endpoints.Categories;

/// <summary>
/// POST endpoints for creating categories.
/// - POST / - Create a single category
/// - POST /bulk - Create multiple categories in a single transaction
/// </summary>
public static class CreateCategoryEndpoint
{
    public static RouteGroupBuilder MapCreateCategory(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateCategoryHandler)
            .WithName("CreateCategory")
            .WithDescription("Create a single category")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/bulk", CreateCategoriesBulkHandler)
            .WithName("BulkCreateCategories")
            .WithDescription("Create multiple categories in a single transaction using AddRange")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        return group;
    }

    /// <summary>
    /// POST / - Create a single category
    /// </summary>
    private static async Task<IResult> CreateCategoryHandler(
        Category category,
        AppDbContext db)
    {
        category.CreatedDate = DateTime.UtcNow;
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return Results.Created($"/categories/{category.Id}", category);
    }

    /// <summary>
    /// POST /bulk - Create multiple categories
    /// </summary>
    private static async Task<IResult> CreateCategoriesBulkHandler(
        List<Category> categories,
        AppDbContext db)
    {
        if (categories == null || categories.Count == 0)
            return Results.BadRequest("Category list cannot be empty");

        var now = DateTime.UtcNow;
        foreach (var category in categories)
        {
            category.CreatedDate = now;
        }

        db.Categories.AddRange(categories);
        await db.SaveChangesAsync();

        return Results.Created("/categories/bulk", new
        {
            Count = categories.Count,
            Message = $"Created {categories.Count} categories",
            CategoryIds = categories.Select(c => c.Id).ToList()
        });
    }
}
