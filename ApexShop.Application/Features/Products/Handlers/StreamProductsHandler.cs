using ApexShop.API.Configuration;
using ApexShop.Application.DTOs;
using ApexShop.API.Extensions;
using ApexShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApexShop.Application.Features.Products.Handlers;

/// <summary>
/// Handler for GET /products/stream - Streaming endpoint with content negotiation
/// Supports MessagePack, NDJSON, or JSON based on Accept header
/// ✅ OPTIMIZED: Factory pattern + configurable flush interval for optimal latency/throughput
/// ✅ CAPPED: Max 10K items to eliminate variance (prevents runaway exports)
/// </summary>
public static class StreamProductsHandler
{
    public static IResult Handle(
        HttpContext context,
        AppDbContext db,
        IOptions<StreamingOptions> streamingOptionsAccessor,
        int? categoryId = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        bool? inStock = null)
    {
        const int MAX_STREAMING_ITEMS = 10_000;
        var streamingOptions = streamingOptionsAccessor.Value;

        var query = db.Products.AsNoTracking();

        // Apply optional filters
        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (minPrice.HasValue)
            query = query.Where(p => p.Price >= minPrice.Value);

        if (maxPrice.HasValue)
            query = query.Where(p => p.Price <= maxPrice.Value);

        if (inStock.HasValue && inStock.Value)
            query = query.Where(p => p.Stock > 0);

        var products = query
            .TagWith("GET /products/stream - Stream all products with filters (constant memory)")
            .OrderBy(p => p.Id)
            .Take(MAX_STREAMING_ITEMS)  // ✅ Hard cap on streaming size
            .Select(p => new ProductListDto(
                p.Id,
                p.Name,
                p.Price,
                p.Stock,
                p.CategoryId))
            .AsAsyncEnumerable();

        // ✅ OPTIMIZED: Factory pattern eliminates repeated header parsing, uses configured flush interval
        return context.StreamAs(products, streamingOptions.FlushInterval);
    }
}
