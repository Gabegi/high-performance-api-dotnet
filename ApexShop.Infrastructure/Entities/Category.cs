namespace ApexShop.Infrastructure.Entities;

public class Category
{
    public short Id { get; set; } // smallint for PostgreSQL compatibility
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation property
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
