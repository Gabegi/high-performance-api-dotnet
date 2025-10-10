namespace ApexShop.Domain.Entities;

public class Review
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int UserId { get; set; }
    public short Rating { get; set; } // smallint for PostgreSQL compatibility (1-5 rating scale)
    public string? Comment { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool IsVerifiedPurchase { get; set; } = false;

    // Navigation properties
    public Product Product { get; set; } = null!;
    public User User { get; set; } = null!;
}
