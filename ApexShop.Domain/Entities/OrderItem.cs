namespace ApexShop.Domain.Entities;

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public short Quantity { get; set; } // smallint: -32,768 to 32,767 (sufficient for order quantities)
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; } // Will be computed column

    // Navigation properties
    public Order Order { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
