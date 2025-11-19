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
/// Handlers for retrieving categories with various pagination and export strategies.
/// </summary>
public static class GetCategories
{
    /// <summary>
    /// GET / - Standard offset-based pagination
    /// Returns categories with basic pagination metadata.
    /// </summary>
    public static async Task<IResult> GetCategoriesHandler(
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

        var totalCount = await CompiledQueries.GetCategoryCount(db);

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
    /// Returns PagedResult with metadata including HasPrevious and HasNext.
    /// </summary>
    public static async Task<IResult> GetCategoriesV2Handler(
        PaginationParams pagination,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.Categories
            .AsNoTracking()
            .TagWith("GET /categories/v2 - List categories with standardized pagination")
            .OrderBy(c => c.Id);

        var result = await query
            .Select(c => new CategoryListDto(
                c.Id,
                c.Name,
                c.Description))
            .ToPagedListAsync(pagination.Page, pagination.PageSize, cancellationToken);

        return Results.Ok(result);
    }

    /// <summary>
    /// GET /cursor - Keyset (Cursor-based) Pagination
    /// More efficient than offset-based pagination for large datasets.
    /// </summary>
    public static async Task<IResult> GetCategoriesCursorHandler(
        AppDbContext db,
        int? cursor = null,
        int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Categories
            .AsNoTracking()
            .TagWith("GET /categories/cursor - Keyset pagination")
            .OrderBy(c => c.Id);

        if (cursor.HasValue)
        {
            query = query.Where(c => c.Id > cursor.Value).OrderBy(c => c.Id);
        }

        var categories = await query
            .Take(pageSize + 1)
            .Select(c => new CategoryListDto(
                c.Id,
                c.Name,
                c.Description))
            .ToListAsync();

        var hasMore = categories.Count > pageSize;
        if (hasMore)
        {
            categories = categories.Take(pageSize).ToList();
        }

        var nextCursor = hasMore ? categories.Last().Id : (int?)null;

        return Results.Ok(new
        {
            Data = categories,
            NextCursor = nextCursor,
            HasMore = hasMore,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// GET /stream - Streaming endpoint with content negotiation
    /// Supports MessagePack, NDJSON, or JSON based on Accept header.
    /// </summary>
    public static IResult StreamCategoriesHandler(
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

        return context.StreamAs(categories);
    }

    /// <summary>
    /// GET /export/ndjson - NDJSON Export
    /// Exports categories in newline-delimited JSON format for efficient large exports.
    /// </summary>
    public static async Task<IResult> ExportCategoriesNdjsonHandler(
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
