using ApexShop.Application.DTOs;
using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;

namespace ApexShop.API.Endpoints.Categories;

/// <summary>
/// GET /{id} endpoint for retrieving a single category by ID.
/// Uses compiled queries for optimal performance.
/// </summary>
public static class GetCategoryByIdEndpoint
{
    public static RouteGroupBuilder MapGetCategoryById(this RouteGroupBuilder group)
    {
        group.MapGet("/{id}", GetCategoryByIdHandler)
            .WithName("GetCategoryById")
            .WithDescription("Get a single category by ID");

        return group;
    }

    /// <summary>
    /// GET /{id} - Retrieve a single category by ID
    /// </summary>
    private static async Task<IResult> GetCategoryByIdHandler(
        int id,
        AppDbContext db)
    {
        var category = await CompiledQueries.GetCategoryById(db, id);
        if (category is null)
            return Results.NotFound();

        return Results.Ok(new CategoryDto(
            category.Id,
            category.Name,
            category.Description,
            category.CreatedDate));
    }
}
