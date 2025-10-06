namespace ApexShop.API.DTOs;

/// <summary>
/// DTO for detailed review view (GET by ID)
/// </summary>
public record ReviewDto(
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
public record ReviewListDto(
    int Id,
    int ProductId,
    int UserId,
    int Rating,
    bool IsVerifiedPurchase
);
