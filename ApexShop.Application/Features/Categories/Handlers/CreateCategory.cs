using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using ApexShop.API.DTOs;
using ApexShop.API.Models.Pagination;
using ApexShop.API.Extensions;
using ApexShop.API.JsonContext;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace ApexShop.Application.Features.Categories.Handlers;

/// <summary>
/// Handlers for creating categories.
/// Supports single and bulk creation operations.
/// </summary>
public static class CreateCategory
{
    /// <summary>
    /// POST / - Create a single category
    /// Returns 201 Created with the created category.
    /// </summary>
    public static async Task<IResult> CreateCategoryHandler(
        Category category,
        AppDbContext db,
        IOutputCacheStore cache)
    {
        category.CreatedDate = DateTime.UtcNow;
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        await cache.EvictByTagAsync("categories", default);

        return Results.Created($"/categories/{category.Id}", category);
    }

    /// <summary>
    /// POST /bulk - Create multiple categories
    /// Uses AddRange for efficient batch insertion.
    /// Returns 201 Created with count and IDs of created categories.
    /// </summary>
    public static async Task<IResult> CreateCategoriesBulkHandler(
        List<Category> categories,
        AppDbContext db,
        IOutputCacheStore cache)
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

        await cache.EvictByTagAsync("categories", default);

        return Results.Created("/categories/bulk", new
        {
            Count = categories.Count,
            Message = $"Created {categories.Count} categories",
            CategoryIds = categories.Select(c => c.Id).ToList()
        });
    }
}
