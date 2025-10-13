using ApexShop.Infrastructure.Entities;
using ApexShop.Infrastructure.Enums;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.Infrastructure.Data.Seeds;

public static class DatabaseSeeder
{
    public static void SeedData(ModelBuilder modelBuilder)
    {
        var random = new Random(42); // Fixed seed for reproducibility
        var baseDate = new DateTime(2024, 1, 1);

        // Seed Categories (15)
        SeedCategories(modelBuilder, baseDate);

        // Seed Users (1,000)
        var users = SeedUsers(modelBuilder, random, baseDate);

        // Seed Products (10,000)
        var products = SeedProducts(modelBuilder, random, baseDate);

        // Seed Orders (5,000)
        var orders = SeedOrders(modelBuilder, random, baseDate);

        // Seed OrderItems (15,000 avg)
        SeedOrderItems(modelBuilder, random, orders, products);

        // Seed Reviews (20,000)
        SeedReviews(modelBuilder, random, baseDate);
    }

    private static void SeedCategories(ModelBuilder modelBuilder, DateTime baseDate)
    {
        var categories = new[]
        {
            new Category { Id = 1, Name = "Electronics", Description = "Electronic devices and accessories", CreatedDate = baseDate },
            new Category { Id = 2, Name = "Clothing", Description = "Apparel and fashion items", CreatedDate = baseDate },
            new Category { Id = 3, Name = "Books", Description = "Books and publications", CreatedDate = baseDate },
            new Category { Id = 4, Name = "Home & Garden", Description = "Home improvement and garden supplies", CreatedDate = baseDate },
            new Category { Id = 5, Name = "Sports & Outdoors", Description = "Sports equipment and outdoor gear", CreatedDate = baseDate },
            new Category { Id = 6, Name = "Toys & Games", Description = "Toys and gaming products", CreatedDate = baseDate },
            new Category { Id = 7, Name = "Health & Beauty", Description = "Health and beauty products", CreatedDate = baseDate },
            new Category { Id = 8, Name = "Automotive", Description = "Auto parts and accessories", CreatedDate = baseDate },
            new Category { Id = 9, Name = "Food & Grocery", Description = "Food and grocery items", CreatedDate = baseDate },
            new Category { Id = 10, Name = "Office Supplies", Description = "Office and school supplies", CreatedDate = baseDate },
            new Category { Id = 11, Name = "Pet Supplies", Description = "Pet food and accessories", CreatedDate = baseDate },
            new Category { Id = 12, Name = "Music & Instruments", Description = "Musical instruments and equipment", CreatedDate = baseDate },
            new Category { Id = 13, Name = "Movies & TV", Description = "Movies and TV shows", CreatedDate = baseDate },
            new Category { Id = 14, Name = "Baby Products", Description = "Baby care and nursery items", CreatedDate = baseDate },
            new Category { Id = 15, Name = "Jewelry", Description = "Jewelry and watches", CreatedDate = baseDate }
        };

        modelBuilder.Entity<Category>().HasData(categories);
    }

    private static List<User> SeedUsers(ModelBuilder modelBuilder, Random random, DateTime baseDate)
    {
        var users = new List<User>();
        var firstNames = new[] { "John", "Jane", "Mike", "Sarah", "David", "Emma", "Chris", "Lisa", "Tom", "Anna" };
        var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez" };

        for (int i = 1; i <= 1000; i++)
        {
            users.Add(new User
            {
                Id = i,
                Email = $"user{i}@apexshop.com",
                PasswordHash = $"HASH{i:D6}", // Placeholder hash
                FirstName = firstNames[i % firstNames.Length],
                LastName = lastNames[(i / 10) % lastNames.Length],
                PhoneNumber = $"+1{random.Next(200, 999)}{random.Next(100, 999)}{random.Next(1000, 9999)}",
                CreatedDate = baseDate.AddDays(-random.Next(0, 365)),
                LastLoginDate = baseDate.AddDays(-random.Next(0, 30)),
                IsActive = random.NextDouble() > 0.05 // 95% active
            });
        }

        modelBuilder.Entity<User>().HasData(users);
        return users;
    }

    private static List<Product> SeedProducts(ModelBuilder modelBuilder, Random random, DateTime baseDate)
    {
        var products = new List<Product>();
        var productNames = new[] { "Pro", "Ultra", "Premium", "Deluxe", "Standard", "Basic", "Advanced", "Elite", "Master", "Super" };
        var productTypes = new[] { "Widget", "Gadget", "Tool", "Device", "Kit", "Set", "Pack", "Bundle", "Collection", "System" };

        for (int i = 1; i <= 10000; i++)
        {
            var categoryId = (i % 15) + 1;
            var name = $"{productNames[i % productNames.Length]} {productTypes[(i / 10) % productTypes.Length]} #{i}";
            var price = Math.Round((decimal)(random.NextDouble() * 999 + 1), 2);
            var stock = random.Next(0, 1000);

            products.Add(new Product
            {
                Id = i,
                Name = name,
                Description = $"High-quality {name.ToLower()} for enhanced performance and reliability",
                Price = price,
                Stock = (short)stock,
                CategoryId = (short)categoryId,
                CreatedDate = baseDate.AddDays(-random.Next(0, 365))
            });
        }

        modelBuilder.Entity<Product>().HasData(products);
        return products;
    }

    private static List<Order> SeedOrders(ModelBuilder modelBuilder, Random random, DateTime baseDate)
    {
        var orders = new List<Order>();
        var statuses = new[] { OrderStatus.Pending, OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Delivered, OrderStatus.Cancelled };

        for (int i = 1; i <= 5000; i++)
        {
            var orderDate = baseDate.AddDays(-random.Next(0, 365));
            var status = statuses[random.Next(statuses.Length)];
            var userId = random.Next(1, 1001);

            orders.Add(new Order
            {
                Id = i,
                UserId = userId,
                OrderDate = orderDate,
                TotalAmount = 0, // Will be calculated from items
                Status = status,
                ShippingAddress = $"{random.Next(100, 9999)} Main St, City {random.Next(1, 100)}, State {random.Next(1, 50):D2}, {random.Next(10000, 99999)}",
                TrackingNumber = status == OrderStatus.Shipped || status == OrderStatus.Delivered ? $"TRK{i:D10}" : null,
                ShippedDate = status == OrderStatus.Shipped || status == OrderStatus.Delivered ? orderDate.AddDays(random.Next(1, 3)) : null,
                DeliveredDate = status == OrderStatus.Delivered ? orderDate.AddDays(random.Next(4, 10)) : null
            });
        }

        modelBuilder.Entity<Order>().HasData(orders);
        return orders;
    }

    private static void SeedOrderItems(ModelBuilder modelBuilder, Random random, List<Order> orders, List<Product> products)
    {
        var orderItems = new List<OrderItem>();
        int orderItemId = 1;

        for (int orderId = 1; orderId <= 5000; orderId++)
        {
            var itemCount = random.Next(1, 6); // 1-5 items per order
            decimal orderTotal = 0;

            for (int j = 0; j < itemCount; j++)
            {
                var productId = random.Next(1, 10001);
                var product = products[productId - 1];
                var quantity = random.Next(1, 5);
                var unitPrice = product.Price;
                var totalPrice = unitPrice * quantity;
                orderTotal += totalPrice;

                orderItems.Add(new OrderItem
                {
                    Id = orderItemId++,
                    OrderId = orderId,
                    ProductId = productId,
                    Quantity = (short)quantity,
                    UnitPrice = unitPrice,
                    TotalPrice = totalPrice
                });
            }

            // Update order total amount
            orders[orderId - 1].TotalAmount = Math.Round(orderTotal, 2);
        }

        modelBuilder.Entity<OrderItem>().HasData(orderItems);
    }

    private static void SeedReviews(ModelBuilder modelBuilder, Random random, DateTime baseDate)
    {
        var reviews = new List<Review>();
        var comments = new[]
        {
            "Great product, highly recommend!",
            "Good quality for the price.",
            "Not what I expected, but okay.",
            "Excellent! Will buy again.",
            "Poor quality, disappointed.",
            "Amazing! Exceeded expectations.",
            "Average product, nothing special.",
            "Fast shipping, great service!",
            "Would not recommend.",
            "Perfect! Exactly what I needed."
        };

        for (int i = 1; i <= 20000; i++)
        {
            var productId = random.Next(1, 10001);
            var userId = random.Next(1, 1001);
            var rating = random.Next(1, 6); // 1-5 stars
            var isVerified = random.NextDouble() > 0.3; // 70% verified purchases

            reviews.Add(new Review
            {
                Id = i,
                ProductId = productId,
                UserId = userId,
                Rating = (short)rating,
                Comment = comments[random.Next(comments.Length)],
                CreatedDate = baseDate.AddDays(-random.Next(0, 365)),
                IsVerifiedPurchase = isVerified
            });
        }

        modelBuilder.Entity<Review>().HasData(reviews);
    }
}
