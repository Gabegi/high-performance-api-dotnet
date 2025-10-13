using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.Infrastructure.Queries;

/// <summary>
/// Pre-compiled EF Core queries for frequently-used operations.
/// Compiled queries provide 30-50% performance improvement by avoiding LINQ-to-SQL translation overhead on every call.
/// These queries return domain entities - the API layer handles mapping to DTOs.
/// </summary>
public static class CompiledQueries
{
    // ========================================
    // GET BY ID QUERIES
    // ========================================

    /// <summary>
    /// Get product by ID - Compiled query
    /// Hot-path: Product detail page
    /// </summary>
    public static readonly Func<AppDbContext, int, Task<Product?>> GetProductById =
        EF.CompileAsyncQuery((AppDbContext db, int id) =>
            db.Products
                .AsNoTracking()
                .TagWith("GET product by ID [COMPILED]")
                .Where(p => p.Id == id)
                .FirstOrDefault());

    /// <summary>
    /// Get category by ID - Compiled query
    /// Hot-path: Category detail page
    /// </summary>
    public static readonly Func<AppDbContext, int, Task<Category?>> GetCategoryById =
        EF.CompileAsyncQuery((AppDbContext db, int id) =>
            db.Categories
                .AsNoTracking()
                .TagWith("GET category by ID [COMPILED]")
                .Where(c => c.Id == id)
                .FirstOrDefault());

    /// <summary>
    /// Get order by ID - Compiled query
    /// Hot-path: Order detail page
    /// </summary>
    public static readonly Func<AppDbContext, int, Task<Order?>> GetOrderById =
        EF.CompileAsyncQuery((AppDbContext db, int id) =>
            db.Orders
                .AsNoTracking()
                .TagWith("GET order by ID [COMPILED]")
                .Where(o => o.Id == id)
                .FirstOrDefault());

    /// <summary>
    /// Get user by ID - Compiled query
    /// Hot-path: User profile page
    /// </summary>
    public static readonly Func<AppDbContext, int, Task<User?>> GetUserById =
        EF.CompileAsyncQuery((AppDbContext db, int id) =>
            db.Users
                .AsNoTracking()
                .TagWith("GET user by ID [COMPILED]")
                .Where(u => u.Id == id)
                .FirstOrDefault());

    /// <summary>
    /// Get review by ID - Compiled query
    /// Hot-path: Review detail page
    /// </summary>
    public static readonly Func<AppDbContext, int, Task<Review?>> GetReviewById =
        EF.CompileAsyncQuery((AppDbContext db, int id) =>
            db.Reviews
                .AsNoTracking()
                .TagWith("GET review by ID [COMPILED]")
                .Where(r => r.Id == id)
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
    public static readonly Func<AppDbContext, int, Task<List<Product>>> GetProductsByCategory =
        EF.CompileAsyncQuery((AppDbContext db, int categoryId) =>
            db.Products
                .AsNoTracking()
                .TagWith("GET products by category [COMPILED]")
                .Where(p => p.CategoryId == categoryId)
                .OrderBy(p => p.Id)
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
    public static readonly Func<AppDbContext, int, Task<List<Order>>> GetOrdersByUser =
        EF.CompileAsyncQuery((AppDbContext db, int userId) =>
            db.Orders
                .AsNoTracking()
                .TagWith("GET orders by user [COMPILED]")
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToList());

    /// <summary>
    /// Get reviews by product ID - Compiled query
    /// Hot-path: Product detail page reviews
    /// </summary>
    public static readonly Func<AppDbContext, int, Task<List<Review>>> GetReviewsByProduct =
        EF.CompileAsyncQuery((AppDbContext db, int productId) =>
            db.Reviews
                .AsNoTracking()
                .TagWith("GET reviews by product [COMPILED]")
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.CreatedDate)
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
