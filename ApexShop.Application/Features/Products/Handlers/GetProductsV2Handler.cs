using ApexShop.API.Models.Pagination;
using ApexShop.Infrastructure.Data;
using ApexShop.API.DTOs;
using ApexShop.API.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.Application.Features.Products.Handlers;

/// <summary>
/// Handler for GET /products/v2 - Improved pagination with standardized response format
/// </summary>
public static class GetProductsV2Handler
{
    public static async Task<IResult> Handle(
        PaginationParams pagination,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.Products
            .AsNoTracking()
            .TagWith("GET /products/v2 - List products with standardized pagination")
            .OrderBy(p => p.Id); // Required for consistent pagination

        // Note: ToPagedListAsync runs COUNT(*) on every request
        // For frequently accessed endpoints, consider caching the count separately
        var result = await query
            .Select(p => new ProductListDto(
                p.Id,
                p.Name,
                p.Price,
                p.Stock,
                p.CategoryId))
            .ToPagedListAsync(pagination.Page, pagination.PageSize, cancellationToken);

        return Results.Ok(result);
    }
}
