namespace ApexShop.API.DTOs;

/// <summary>
/// Generic paginated result wrapper for list API responses.
/// </summary>
public class PaginatedResult<T>
{
    public List<T>? Data { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
