using MessagePack;

namespace ApexShop.Application.DTOs;

/// <summary>
/// DTO for detailed review view (GET by ID)
/// </summary>
[MessagePackObject(true)]
public partial record ReviewDto(
    int Id,
    int ProductId,
    int UserId,
    int Rating,
    string? Comment,
    DateTime CreatedDate,
    bool IsVerifiedPurchase
);

/// <summary>
/// Lightweight DTO for review lists (GET all)
/// </summary>
[MessagePackObject(true)]
public partial record ReviewListDto(
    int Id,
    int ProductId,
    int UserId,
    int Rating,
    bool IsVerifiedPurchase
);
