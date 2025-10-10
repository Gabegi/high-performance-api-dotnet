namespace ApexShop.Domain.Enums;

/// <summary>
/// Order status stored as smallint for PostgreSQL compatibility
/// </summary>
public enum OrderStatus : short
{
    Pending = 0,
    Processing = 1,
    Shipped = 2,
    Delivered = 3,
    Cancelled = 4
}
