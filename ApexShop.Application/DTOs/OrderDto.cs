using MessagePack;

namespace ApexShop.Application.DTOs;

/// <summary>
/// DTO for detailed order view (GET by ID)
/// </summary>
[MessagePackObject(true)]
public partial record OrderDto(
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
[MessagePackObject(true)]
public partial record OrderListDto(
    int Id,
    int UserId,
    DateTime OrderDate,
    string Status,
    decimal TotalAmount
);
