namespace ApexShop.API.Models.Pagination;

/// <summary>
/// Request parameters for pagination with built-in validation.
/// Enforces maximum page size and provides sensible defaults.
/// </summary>
public class PaginationParams
{
    private const int MaxPageSize = 100;
    private int _pageSize = 20;

    /// <summary>
    /// Current page number (1-based indexing).
    /// Default: 1
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Number of items per page.
    /// Default: 20
    /// Max: 100 (enforced by setter)
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
    }

    /// <summary>
    /// Validates pagination parameters.
    /// Ensures page is at least 1 and pageSize is positive.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsValid()
    {
        return Page >= 1 && _pageSize >= 1;
    }

    /// <summary>
    /// Calculates the number of records to skip based on page and page size.
    /// </summary>
    public int GetSkip() => (Page - 1) * PageSize;
}
