using MessagePack;

namespace ApexShop.API.DTOs;

/// <summary>
/// DTO for detailed category view (GET by ID)
/// </summary>
[MessagePackObject(true)]
public partial record CategoryDto(
    int Id,
    string Name,
    string? Description,
    DateTime CreatedDate
);

/// <summary>
/// Lightweight DTO for category lists (GET all)
/// </summary>
[MessagePackObject(true)]
public partial record CategoryListDto(
    int Id,
    string Name,
    string? Description
);
