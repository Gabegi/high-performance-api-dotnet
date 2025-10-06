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
                    o.Status,
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
}
