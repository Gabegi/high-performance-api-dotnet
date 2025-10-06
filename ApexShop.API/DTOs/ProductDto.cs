namespace ApexShop.API.DTOs;

/// <summary>
/// DTO for detailed product view (GET by ID)
/// </summary>
public record ProductDto(
    int Id,
    string Name,
    string? Description,
    decimal Price,
    int Stock,
    int CategoryId,
    DateTime CreatedDate,
    DateTime? UpdatedDate
);

/// <summary>
/// Lightweight DTO for product lists (GET all) - excludes description and timestamps for performance
/// </summary>
public record ProductListDto(
    int Id,
    string Name,
    decimal Price,
    int Stock,
    int CategoryId
);
