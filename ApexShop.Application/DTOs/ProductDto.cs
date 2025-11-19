using MessagePack;

namespace ApexShop.Application.DTOs;

/// <summary>
/// DTO for detailed product view (GET by ID)
/// </summary>
[MessagePackObject(true)]
public partial record ProductDto(
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
[MessagePackObject(true)]
public partial record ProductListDto(
    int Id,
    string Name,
    decimal Price,
    int Stock,
    int CategoryId
);
