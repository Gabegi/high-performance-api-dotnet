namespace ApexShop.API.Models.Pagination;

/// <summary>
/// Generic paginated response wrapper.
/// Contains the data plus pagination metadata to help clients navigate results.
/// Immutable after construction to prevent accidental modifications.
/// </summary>
/// <typeparam name="T">The type of items in the paginated result.</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// The page data/items (immutable read-only collection).
    /// </summary>
    public IReadOnlyList<T> Data { get; init; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// Total count of all items across all pages.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Total number of pages based on total count and page size.
    /// </summary>
    public int TotalPages { get; init; }

    /// <summary>
    /// Indicates if there is a previous page.
    /// </summary>
    public bool HasPrevious => Page > 1;

    /// <summary>
    /// Indicates if there is a next page.
    /// </summary>
    public bool HasNext => Page < TotalPages;

    /// <summary>
    /// Creates a new paginated result.
    /// </summary>
    /// <param name="data">The items for this page (will be made read-only).</param>
    /// <param name="count">The total count of all items.</param>
    /// <param name="page">The current page number.</param>
    /// <param name="pageSize">The page size.</param>
    public PagedResult(List<T> data, int count, int page, int pageSize)
    {
        Data = data?.AsReadOnly() ?? new List<T>().AsReadOnly();
        TotalCount = count;
        Page = page;
        PageSize = pageSize;
        TotalPages = pageSize > 0 ? (int)Math.Ceiling(count / (double)pageSize) : 0;
    }

    /// <summary>
    /// Creates an empty paginated result (no items).
    /// </summary>
    public PagedResult()
    {
        Data = new List<T>().AsReadOnly();
        TotalCount = 0;
        Page = 1;
        PageSize = 0;
        TotalPages = 0;
    }
}
