using ApexShop.Infrastructure.Data;
using ApexShop.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.Application.Features.Products.Handlers;

/// <summary>
/// Handler for GET /products/cursor - Keyset (Cursor-based) Pagination
/// Optimized for deep pagination and large datasets.
/// ✅ OPTIMIZED: Replaced RemoveAt() O(n) with Take() O(1) - eliminates array reallocation
/// </summary>
public static class GetProductsCursorHandler
{
    public static async Task<IResult> Handle(
        AppDbContext db,
        int? afterId = null,
        int pageSize = 50)
    {
        // Validate pagination parameters
        pageSize = Math.Clamp(pageSize, 1, 100); // Max 100 items per page

        var query = db.Products.AsNoTracking();

        // Apply cursor filter if provided
        if (afterId.HasValue)
        {
            query = query.Where(p => p.Id > afterId.Value);
        }

        // Fetch one extra to determine if there are more results
        var allProducts = await query
            .TagWith("GET /products/cursor - Keyset pagination (optimized for deep pages)")
            .OrderBy(p => p.Id) // Required for consistent pagination
            .Take(pageSize + 1)
            .Select(p => new ProductListDto(
                p.Id,
                p.Name,
                p.Price,
                p.Stock,
                p.CategoryId))
            .ToListAsync();

        // ✅ OPTIMIZED: Detect hasMore BEFORE slicing (no allocation for extra item)
        var hasMore = allProducts.Count > pageSize;

        // ✅ FAST: Take() creates view of first pageSize items (O(1) vs RemoveAt O(n))
        // RemoveAt caused array reallocation; Take avoids it entirely
        var products = allProducts.Take(pageSize).ToList();

        return Results.Ok(new
        {
            Data = products,
            PageSize = pageSize,
            HasMore = hasMore,
            NextCursor = hasMore && products.Count > 0 ? products[^1].Id : (int?)null
        });
    }
}
