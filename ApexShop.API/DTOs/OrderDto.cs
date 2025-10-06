namespace ApexShop.API.DTOs;

/// <summary>
/// DTO for detailed order view (GET by ID)
/// </summary>
public record OrderDto(
    int Id,
    int UserId,
    DateTime OrderDate,
    string Status,
    decimal TotalAmount,
    string ShippingAddress,
    string? TrackingNumber,
    DateTime? ShippedDate,
    DateTime? DeliveredDate
);

/// <summary>
/// Lightweight DTO for order lists (GET all) - excludes shipping details for performance
/// </summary>
public record OrderListDto(
    int Id,
    int UserId,
    DateTime OrderDate,
    string Status,
    decimal TotalAmount
);
