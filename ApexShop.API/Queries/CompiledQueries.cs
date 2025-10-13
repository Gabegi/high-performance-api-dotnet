using ApexShop.API.DTOs;
using ApexShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.API.Queries;

/// <summary>
/// Pre-compiled EF Core queries for frequently-used operations.
/// Compiled queries provide 30-50% performance improvement by avoiding LINQ-to-SQL translation overhead on every call.
/// </summary>
public static class CompiledQueries
{
    /// <summary>
    /// Get product by ID - Compiled query
    /// </summary>
    public static readonly Func<AppDbContext, int, Task<ProductDto?>> GetProductById =
        EF.CompileAsyncQuery((AppDbContext db, int id) =>
            db.Products
                .AsNoTracking()
                .TagWith("GET /products/{id} - Get product by ID [COMPILED]")
                .Where(p => p.Id == id)
                .Select(p => new ProductDto(
                    p.Id,
                    p.Name,
                    p.Description,
                    p.Price,
                    p.Stock,
                    p.CategoryId,
                    p.CreatedDate,
                    p.UpdatedDate))
                .FirstOrDefault());

    /// <summary>
    /// Get category by ID - Compiled query
    /// </summary>
    public static readonly Func<AppDbContext, int, Task<CategoryDto?>> GetCategoryById =
        EF.CompileAsyncQuery((AppDbContext db, int id) =>
            db.Categories
                .AsNoTracking()
                .TagWith("GET /categories/{id} - Get category by ID [COMPILED]")
                .Where(c => c.Id == id)
                .Select(c => new CategoryDto(
                    c.Id,
                    c.Name,
                    c.Description,
                    c.CreatedDate))
                .FirstOrDefault());

    /// <summary>
    /// Get order by ID - Compiled query
    /// </summary>
    public static readonly Func<AppDbContext, int, Task<OrderDto?>> GetOrderById =
        EF.CompileAsyncQuery((AppDbContext db, int id) =>
            db.Orders
                .AsNoTracking()
                .TagWith("GET /orders/{id} - Get order by ID [COMPILED]")
                .Where(o => o.Id == id)
                .Select(o => new OrderDto(
                    o.Id,
                    o.UserId,
                    o.OrderDate,
                    o.Status.ToString(),
                    o.TotalAmount,
                    o.ShippingAddress,
                    o.TrackingNumber,
                    o.ShippedDate,
                    o.DeliveredDate))
                .FirstOrDefault());

    /// <summary>
    /// Get user by ID - Compiled query
    /// </summary>
    public static readonly Func<AppDbContext, int, Task<UserDto?>> GetUserById =
        EF.CompileAsyncQuery((AppDbContext db, int id) =>
            db.Users
                .AsNoTracking()
                .TagWith("GET /users/{id} - Get user by ID [COMPILED]")
                .Where(u => u.Id == id)
                .Select(u => new UserDto(
                    u.Id,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.PhoneNumber,
                    u.IsActive,
                    u.CreatedDate,
                    u.LastLoginDate))
                .FirstOrDefault());

    /// <summary>
    /// Get review by ID - Compiled query
    /// </summary>
    public static readonly Func<AppDbContext, int, Task<ReviewDto?>> GetReviewById =
        EF.CompileAsyncQuery((AppDbContext db, int id) =>
            db.Reviews
                .AsNoTracking()
                .TagWith("GET /reviews/{id} - Get review by ID [COMPILED]")
                .Where(r => r.Id == id)
                .Select(r => new ReviewDto(
                    r.Id,
                    r.ProductId,
                    r.UserId,
                    r.Rating,
                    r.Comment,
                    r.CreatedDate,
                    r.IsVerifiedPurchase))
                .FirstOrDefault());

    // ========================================
    // COUNT QUERIES - Frequently called in pagination
    // ========================================

    /// <summary>
    /// Get total product count - Compiled query
    /// Hot-path: Called on every paginated product list request
    /// </summary>
    public static readonly Func<AppDbContext, Task<int>> GetProductCount =
        EF.CompileAsyncQuery((AppDbContext db) =>
            db.Products.Count());

    /// <summary>
    /// Get total order count - Compiled query
    /// Hot-path: Called on every paginated order list request
    /// </summary>
    public static readonly Func<AppDbContext, Task<int>> GetOrderCount =
        EF.CompileAsyncQuery((AppDbContext db) =>
            db.Orders.Count());

    /// <summary>
    /// Get total user count - Compiled query
    /// Hot-path: Called on every paginated user list request
    /// </summary>
    public static readonly Func<AppDbContext, Task<int>> GetUserCount =
        EF.CompileAsyncQuery((AppDbContext db) =>
            db.Users.Count());

    /// <summary>
    /// Get total category count - Compiled query
    /// Hot-path: Called on every paginated category list request
    /// </summary>
    public static readonly Func<AppDbContext, Task<int>> GetCategoryCount =
        EF.CompileAsyncQuery((AppDbContext db) =>
            db.Categories.Count());

    /// <summary>
    /// Get total review count - Compiled query
    /// Hot-path: Called on every paginated review list request
    /// </summary>
    public static readonly Func<AppDbContext, Task<int>> GetReviewCount =
        EF.CompileAsyncQuery((AppDbContext db) =>
            db.Reviews.Count());

    // ========================================
    // COMMON FILTER QUERIES - Frequently used operations
    // ========================================

    /// <summary>
    /// Get products by category - Compiled query
    /// Hot-path: Very common in e-commerce (category browsing)
    /// </summary>
    public static readonly Func<AppDbContext, int, Task<List<ProductListDto>>> GetProductsByCategory =
        EF.CompileAsyncQuery((AppDbContext db, int categoryId) =>
            db.Products
                .AsNoTracking()
                .TagWith("Get products by category [COMPILED]")
                .Where(p => p.CategoryId == categoryId)
                .OrderBy(p => p.Id)
                .Select(p => new ProductListDto(
                    p.Id,
                    p.Name,
                    p.Price,
                    p.Stock,
                    p.CategoryId))
                .ToList());

    /// <summary>
    /// Get in-stock products count by category - Compiled query
    /// Hot-path: Common inventory check
    /// </summary>
    public static readonly Func<AppDbContext, int, Task<int>> GetInStockCountByCategory =
        EF.CompileAsyncQuery((AppDbContext db, int categoryId) =>
            db.Products
                .Where(p => p.CategoryId == categoryId && p.Stock > 0)
                .Count());

    /// <summary>
    /// Get orders by user ID - Compiled query
    /// Hot-path: User dashboard, order history
    /// </summary>
    public static readonly Func<AppDbContext, int, Task<List<OrderListDto>>> GetOrdersByUser =
        EF.CompileAsyncQuery((AppDbContext db, int userId) =>
            db.Orders
                .AsNoTracking()
                .TagWith("Get orders by user [COMPILED]")
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new OrderListDto(
                    o.Id,
                    o.UserId,
                    o.OrderDate,
                    o.Status.ToString(),
                    o.TotalAmount))
                .ToList());

    /// <summary>
    /// Get reviews by product ID - Compiled query
    /// Hot-path: Product detail page reviews
    /// </summary>
    public static readonly Func<AppDbContext, int, Task<List<ReviewListDto>>> GetReviewsByProduct =
        EF.CompileAsyncQuery((AppDbContext db, int productId) =>
            db.Reviews
                .AsNoTracking()
                .TagWith("Get reviews by product [COMPILED]")
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.CreatedDate)
                .Select(r => new ReviewListDto(
                    r.Id,
                    r.ProductId,
                    r.UserId,
                    r.Rating,
                    r.IsVerifiedPurchase))
                .ToList());

    /// <summary>
    /// Get average rating for product - Compiled query
    /// Hot-path: Product cards, search results
    /// </summary>
    public static readonly Func<AppDbContext, int, Task<double?>> GetAverageRatingForProduct =
        EF.CompileAsyncQuery((AppDbContext db, int productId) =>
            db.Reviews
                .Where(r => r.ProductId == productId)
                .Average(r => (double?)r.Rating));

    /// <summary>
    /// Check product availability - Compiled query
    /// Hot-path: Add to cart, checkout validation
    /// </summary>
    public static readonly Func<AppDbContext, int, Task<bool>> IsProductInStock =
        EF.CompileAsyncQuery((AppDbContext db, int productId) =>
            db.Products
                .Where(p => p.Id == productId && p.Stock > 0)
                .Any());
}
