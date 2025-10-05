using ApexShop.Domain.Entities;
using Bogus;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.Infrastructure.Data;

public class DbSeeder
{
    private readonly AppDbContext _context;

    public DbSeeder(AppDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        // Smart check - exits immediately if already seeded
        if (await _context.Products.AnyAsync())
        {
            Console.WriteLine("✓ Database already seeded, skipping...");
            return;
        }

        Console.WriteLine("→ Seeding database for benchmarking...");

        try
        {
            await SeedCategoriesAsync();
            await SeedUsersAsync(3000);      // Realistic customer base
            await SeedProductsAsync(15000);  // Real-world product catalog
            await SeedOrdersAsync(5000);     // Good order history
            await SeedReviewsAsync(12000);   // ~80% products have reviews

            Console.WriteLine("✓ Benchmark database seeded successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Seeding failed: {ex.Message}");
            throw;
        }
    }

    private async Task SeedCategoriesAsync()
    {
        var categories = new List<Category>
        {
            new() { Name = "Electronics", Description = "Electronic devices and accessories", CreatedDate = DateTime.UtcNow },
            new() { Name = "Clothing", Description = "Apparel and fashion items", CreatedDate = DateTime.UtcNow },
            new() { Name = "Books", Description = "Books and publications", CreatedDate = DateTime.UtcNow },
            new() { Name = "Home & Garden", Description = "Home improvement and garden supplies", CreatedDate = DateTime.UtcNow },
            new() { Name = "Sports & Outdoors", Description = "Sports equipment and outdoor gear", CreatedDate = DateTime.UtcNow },
            new() { Name = "Toys & Games", Description = "Toys and gaming products", CreatedDate = DateTime.UtcNow },
            new() { Name = "Health & Beauty", Description = "Health and beauty products", CreatedDate = DateTime.UtcNow },
            new() { Name = "Automotive", Description = "Auto parts and accessories", CreatedDate = DateTime.UtcNow },
            new() { Name = "Food & Grocery", Description = "Food and grocery items", CreatedDate = DateTime.UtcNow },
            new() { Name = "Office Supplies", Description = "Office and school supplies", CreatedDate = DateTime.UtcNow },
            new() { Name = "Pet Supplies", Description = "Pet food and accessories", CreatedDate = DateTime.UtcNow },
            new() { Name = "Music & Instruments", Description = "Musical instruments and equipment", CreatedDate = DateTime.UtcNow },
            new() { Name = "Movies & TV", Description = "Movies and TV shows", CreatedDate = DateTime.UtcNow },
            new() { Name = "Baby Products", Description = "Baby care and nursery items", CreatedDate = DateTime.UtcNow },
            new() { Name = "Jewelry", Description = "Jewelry and watches", CreatedDate = DateTime.UtcNow }
        };

        await _context.Categories.AddRangeAsync(categories);
        await _context.SaveChangesAsync();
        Console.WriteLine($"  → Seeded {categories.Count} categories");
    }

    private async Task SeedUsersAsync(int count)
    {
        var userFaker = new Faker<User>()
            .RuleFor(u => u.Email, f => f.Internet.Email())
            .RuleFor(u => u.PasswordHash, f => f.Random.Hash())
            .RuleFor(u => u.FirstName, f => f.Name.FirstName())
            .RuleFor(u => u.LastName, f => f.Name.LastName())
            .RuleFor(u => u.PhoneNumber, f => f.Phone.PhoneNumber())
            .RuleFor(u => u.CreatedDate, f => f.Date.Past(1))
            .RuleFor(u => u.LastLoginDate, f => f.Date.Recent(30))
            .RuleFor(u => u.IsActive, f => f.Random.Bool(0.95f));

        const int batchSize = 1000;
        for (int i = 0; i < count; i += batchSize)
        {
            var batch = userFaker.Generate(Math.Min(batchSize, count - i));
            await _context.Users.AddRangeAsync(batch);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();
            Console.WriteLine($"  → Seeded {i + batch.Count}/{count} users");
        }
    }

    private async Task SeedProductsAsync(int count)
    {
        var productFaker = new Faker<Product>()
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .RuleFor(p => p.Description, f => f.Commerce.ProductDescription())
            .RuleFor(p => p.Price, f => f.Random.Decimal(10, 1000))
            .RuleFor(p => p.Stock, f => f.Random.Int(0, 1000))
            .RuleFor(p => p.CategoryId, f => f.Random.Int(1, 15))
            .RuleFor(p => p.CreatedDate, f => f.Date.Past(1));

        const int batchSize = 1000;
        for (int i = 0; i < count; i += batchSize)
        {
            var batch = productFaker.Generate(Math.Min(batchSize, count - i));
            await _context.Products.AddRangeAsync(batch);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();
            Console.WriteLine($"  → Seeded {i + batch.Count}/{count} products");
        }
    }

    private async Task SeedOrdersAsync(int count)
    {
        // CRITICAL FIX: Load all data upfront instead of querying in loop
        var userIds = await _context.Users.Select(u => u.Id).ToListAsync();
        var products = await _context.Products
            .Select(p => new { p.Id, p.Price })
            .ToListAsync();
        var statuses = new[] { "Pending", "Processing", "Shipped", "Delivered", "Cancelled" };

        var faker = new Faker();
        var orderFaker = new Faker<Order>()
            .RuleFor(o => o.UserId, f => f.PickRandom(userIds))
            .RuleFor(o => o.OrderDate, f => f.Date.Past(1))
            .RuleFor(o => o.Status, f => f.PickRandom(statuses))
            .RuleFor(o => o.ShippingAddress, f => f.Address.FullAddress())
            .RuleFor(o => o.TrackingNumber, (f, o) =>
                o.Status == "Shipped" || o.Status == "Delivered"
                    ? f.Random.AlphaNumeric(10).ToUpper()
                    : null)
            .RuleFor(o => o.ShippedDate, (f, o) =>
                o.Status == "Shipped" || o.Status == "Delivered"
                    ? o.OrderDate.AddDays(f.Random.Int(1, 3))
                    : null)
            .RuleFor(o => o.DeliveredDate, (f, o) =>
                o.Status == "Delivered"
                    ? o.ShippedDate?.AddDays(f.Random.Int(2, 7))
                    : null);

        const int batchSize = 500; // Smaller batches for orders since they have items
        for (int i = 0; i < count; i += batchSize)
        {
            var batch = orderFaker.Generate(Math.Min(batchSize, count - i));
            var allOrderItems = new List<OrderItem>();

            // Create order items without database queries
            foreach (var order in batch)
            {
                var itemCount = faker.Random.Int(1, 5);
                decimal totalAmount = 0;

                for (int j = 0; j < itemCount; j++)
                {
                    var product = faker.PickRandom(products);
                    var quantity = faker.Random.Int(1, 5);
                    var unitPrice = product.Price;
                    var totalPrice = unitPrice * quantity;
                    totalAmount += totalPrice;

                    var orderItem = new OrderItem
                    {
                        Order = order, // Use navigation property instead of OrderId
                        ProductId = product.Id,
                        Quantity = quantity,
                        UnitPrice = unitPrice,
                        TotalPrice = totalPrice
                    };

                    allOrderItems.Add(orderItem);
                }

                order.TotalAmount = totalAmount;
            }

            // Save orders and items in one transaction
            await _context.Orders.AddRangeAsync(batch);
            await _context.SaveChangesAsync(); // This saves both orders and items
            _context.ChangeTracker.Clear();

            Console.WriteLine($"  → Seeded {i + batch.Count}/{count} orders with {allOrderItems.Count} items");
        }
    }

    private async Task SeedReviewsAsync(int count)
    {
        var userIds = await _context.Users.Select(u => u.Id).ToListAsync();
        var productIds = await _context.Products.Select(p => p.Id).ToListAsync();

        var reviewFaker = new Faker<Review>()
            .RuleFor(r => r.ProductId, f => f.PickRandom(productIds))
            .RuleFor(r => r.UserId, f => f.PickRandom(userIds))
            .RuleFor(r => r.Rating, f => f.Random.Int(1, 5))
            .RuleFor(r => r.Comment, f => f.Rant.Review())
            .RuleFor(r => r.CreatedDate, f => f.Date.Past(1))
            .RuleFor(r => r.IsVerifiedPurchase, f => f.Random.Bool(0.7f));

        const int batchSize = 1000;
        for (int i = 0; i < count; i += batchSize)
        {
            var batch = reviewFaker.Generate(Math.Min(batchSize, count - i));
            await _context.Reviews.AddRangeAsync(batch);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();
            Console.WriteLine($"  → Seeded {i + batch.Count}/{count} reviews");
        }
    }
}
