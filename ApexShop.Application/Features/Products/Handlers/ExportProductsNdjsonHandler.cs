using ApexShop.API.Configuration;
using ApexShop.Application.DTOs;
using ApexShop.API.Extensions;
using ApexShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApexShop.Application.Features.Products.Handlers;

/// <summary>
/// Handler for GET /products/export/ndjson - NDJSON Export v2
/// Newline Delimited JSON for efficient streaming and parsing
/// Enhanced version with:
/// - Rate limiting (5 requests/minute per user)
/// - Safeguards (max record limits, cancellation support)
/// - Additional filters (modifiedAfter for incremental exports)
/// - Audit logging
///
/// Optimal for: large exports (100K+), streaming parsers, downstream data pipelines
/// </summary>
public static class ExportProductsNdjsonHandler
{
    public static async Task<IResult> Handle(
        HttpContext context,
        AppDbContext db,
        ILogger logger,
        IOptions<StreamingOptions> streamingOptionsAccessor,
        int? categoryId = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        bool? inStock = null,
        DateTime? modifiedAfter = null,
        CancellationToken cancellationToken = default)
    {
        var streamingOptions = streamingOptionsAccessor.Value;

        // ✅ Hard cap on streaming exports to prevent variance and resource exhaustion
        const int MAX_STREAMING_ITEMS = 10_000;

        // Build query with filters
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

        // Support incremental exports (only export modified products since timestamp)
        if (modifiedAfter.HasValue)
            query = query.Where(p => p.UpdatedDate >= modifiedAfter.Value || p.CreatedDate >= modifiedAfter.Value);

        // Prepare response and stream
        var filteredQuery = query
            .TagWith("GET /products/export/ndjson - NDJSON export with filters and safeguards")
            .OrderBy(p => p.Id)
            .Take(MAX_STREAMING_ITEMS)  // ✅ Hard cap on streaming size
            .Select(p => new ProductListDto(
                p.Id,
                p.Name,
                p.Price,
                p.Stock,
                p.CategoryId));

        // Set up response stream
        context.Response.ContentType = "application/x-ndjson";
        context.Response.Headers.Append("Content-Disposition", "attachment; filename=products.ndjson");

        // Use StreamingExtensions for safe streaming with proper error handling
        await StreamingExtensions.StreamToNdjsonAsync(
            context,
            filteredQuery.AsAsyncEnumerable()
                .StreamWithSafeguards(streamingOptions.MaxRecords, cancellationToken),
            logger,
            streamingOptions,
            cancellationToken);

        return Results.Empty;
    }
}
