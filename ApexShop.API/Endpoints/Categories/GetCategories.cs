using ApexShop.Application.DTOs;
using ApexShop.API.Extensions;
using ApexShop.API.JsonContext;
using ApexShop.API.Models.Pagination;
using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace ApexShop.API.Endpoints.Categories;

/// <summary>
/// GET endpoints for categories listing and streaming.
/// - GET / - Standard pagination (offset-based)
/// - GET /v2 - Standardized pagination with metadata
/// - GET /stream - Stream all categories with content negotiation
/// - GET /export/ndjson - NDJSON export
/// </summary>
public static class GetCategoriesEndpoint
{
    public static RouteGroupBuilder MapGetCategories(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetCategoriesHandler)
            .CacheOutput("Lists")
            .WithName("GetCategories")
            .WithDescription("List categories with offset-based pagination");

        group.MapGet("/v2", GetCategoriesV2Handler)
            .CacheOutput(policyName: "Lists")
            .WithName("GetCategoriesV2")
            .WithDescription("List categories with standardized pagination. Returns PagedResult with metadata including HasPrevious and HasNext.");

        group.MapGet("/stream", StreamCategoriesHandler)
            .WithName("StreamCategories")
            .WithDescription("Stream all categories with content negotiation (MessagePack, NDJSON, or JSON). Use Accept header to specify format.")
            .Produces(StatusCodes.Status200OK);

        group.MapGet("/export/ndjson", ExportCategoriesNdjsonHandler)
            .WithName("ExportCategoriesNdjson")
            .WithDescription("Export categories as NDJSON - optimal for large exports. Max 50K items.")
            .Produces(StatusCodes.Status200OK);

        return group;
    }

    /// <summary>
    /// GET / - Standard offset-based pagination
    /// </summary>
    private static async Task<IResult> GetCategoriesHandler(
        AppDbContext db,
        int page = 1,
        int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var categories = await db.Categories
            .AsNoTracking()
            .TagWith("GET /categories - List categories with pagination")
            .OrderBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CategoryListDto(
                c.Id,
                c.Name,
                c.Description))
            .ToListAsync();

        var totalCount = await CompiledQueries.GetCategoryCount(db); // ← Using compiled query

        return Results.Ok(new
        {
            Data = categories,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    /// <summary>
    /// GET /v2 - Improved pagination with standardized response format
    /// </summary>
    private static async Task<IResult> GetCategoriesV2Handler(
        [AsParameters] PaginationParams pagination,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.Categories
            .AsNoTracking()
            .TagWith("GET /categories/v2 - List categories with standardized pagination")
            .OrderBy(c => c.Id); // Required for consistent pagination

        // Note: ToPagedListAsync runs COUNT(*) on every request
        // For frequently accessed endpoints, consider caching the count separately
        var result = await query
            .Select(c => new CategoryListDto(
                c.Id,
                c.Name,
                c.Description))
            .ToPagedListAsync(pagination.Page, pagination.PageSize, cancellationToken);

        return Results.Ok(result);
    }

    /// <summary>
    /// GET /stream - Streaming endpoint with content negotiation
    /// Supports MessagePack, NDJSON, or JSON based on Accept header
    /// </summary>
    private static IResult StreamCategoriesHandler(
        HttpContext context,
        AppDbContext db)
    {
        var categories = db.Categories
            .AsNoTracking()
            .TagWith("GET /categories/stream - Stream all categories (constant memory)")
            .OrderBy(c => c.Id)
            .Select(c => new CategoryListDto(
                c.Id,
                c.Name,
                c.Description))
            .AsAsyncEnumerable();

        // Content negotiation: return in client-preferred format (MessagePack, NDJSON, or JSON)
        return context.StreamAs(categories);
    }

    /// <summary>
    /// GET /export/ndjson - NDJSON Export
    /// </summary>
    private static async Task<IResult> ExportCategoriesNdjsonHandler(
        HttpContext context,
        AppDbContext db,
        int limit = 50000,
        CancellationToken cancellationToken = default)
    {
        try
        {
            limit = Math.Clamp(limit, 1, 50000);
            context.Response.ContentType = "application/x-ndjson";
            context.Response.Headers.Append("Content-Disposition", "attachment; filename=categories.ndjson");

            var filteredQuery = db.Categories
                .AsNoTracking()
                .TagWith("GET /categories/export/ndjson - NDJSON export")
                .OrderBy(c => c.Id)
                .Take(limit)
                .Select(c => new CategoryListDto(c.Id, c.Name, c.Description));

            int exportedCount = 0;
            await foreach (var category in filteredQuery.AsAsyncEnumerable().WithCancellation(cancellationToken))
            {
                await JsonSerializer.SerializeAsync(context.Response.Body, category, ApexShopJsonContext.Default.CategoryListDto, cancellationToken);
                await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes("\n"), cancellationToken);
                if (++exportedCount % 100 == 0)
                    await context.Response.Body.FlushAsync(cancellationToken);
            }
            await context.Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            context.Response.HttpContext.Abort();
        }
        catch
        {
            context.Response.HttpContext.Abort();
            throw;
        }

        return Results.Empty;
    }
}
