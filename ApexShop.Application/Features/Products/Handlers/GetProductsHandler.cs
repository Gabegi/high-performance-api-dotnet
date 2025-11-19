using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using ApexShop.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.Application.Features.Products.Handlers;

/// <summary>
/// Handler for GET /products - List products with offset-based pagination
/// </summary>
public static class GetProductsHandler
{
    public static async Task<IResult> Handle(
        AppDbContext db,
        int page = 1,
        int pageSize = 50)
    {
        // Validate pagination parameters
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100); // Max 100 items per page

        var products = await db.Products
            .AsNoTracking()
            .TagWith("GET /products - List products with pagination")
            .OrderBy(p => p.Id) // Required for consistent pagination
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListDto(
                p.Id,
                p.Name,
                p.Price,
                p.Stock,
                p.CategoryId))
            .ToListAsync();

        var totalCount = await CompiledQueries.GetProductCount(db); // ← Using compiled query

        return Results.Ok(new
        {
            Data = products,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }
}
