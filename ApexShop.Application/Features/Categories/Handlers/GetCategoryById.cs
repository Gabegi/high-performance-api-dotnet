using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using ApexShop.Application.DTOs;
using ApexShop.API.Models.Pagination;
using ApexShop.API.Extensions;
using ApexShop.API.JsonContext;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace ApexShop.Application.Features.Categories.Handlers;

/// <summary>
/// Handler for retrieving a single category by ID.
/// Uses compiled queries for optimal performance.
/// </summary>
public static class GetCategoryById
{
    /// <summary>
    /// GET /{id} - Retrieve a single category by ID
    /// Returns the category if found, otherwise returns 404.
    /// </summary>
    public static async Task<IResult> GetCategoryByIdHandler(
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
