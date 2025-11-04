using ApexShop.API.Models.Pagination;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.API.Extensions;

/// <summary>
/// Extension methods for IQueryable to support pagination.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>
    /// Converts any IQueryable to a paginated result.
    ///
    /// IMPORTANT: Query must have .OrderBy() BEFORE calling this method.
    /// Example: query.OrderBy(x => x.Id).ToPagedListAsync(page, pageSize)
    ///
    /// Note: Runs COUNT(*) query on every request.
    /// For frequently accessed endpoints, consider caching the count separately.
    /// </summary>
    /// <typeparam name="T">The type of elements in the query.</typeparam>
    /// <param name="query">The IQueryable to paginate (must be ordered).</param>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A PagedResult containing the items for the specified page and pagination metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown if query is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if page < 1 or pageSize < 1.</exception>
    public static async Task<PagedResult<T>> ToPagedListAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (page < 1)
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be >= 1");
        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "PageSize must be >= 1");

        // Get total count BEFORE pagination (required for TotalPages calculation)
        var count = await query.CountAsync(cancellationToken);

        // Apply pagination using Skip/Take
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, count, page, pageSize);
    }
}
