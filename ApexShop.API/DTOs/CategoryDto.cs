namespace ApexShop.API.DTOs;

/// <summary>
/// DTO for detailed category view (GET by ID)
/// </summary>
public record CategoryDto(
    int Id,
    string Name,
    string? Description,
    DateTime CreatedDate
);

/// <summary>
/// Lightweight DTO for category lists (GET all)
/// </summary>
public record CategoryListDto(
    int Id,
    string Name,
    string? Description
);
