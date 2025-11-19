using ApexShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.Application.Features.Products.Handlers;

/// <summary>
/// Handler for PATCH /products/bulk-update-stock - Bulk update stock for products in a category
/// ExecuteUpdate: Zero memory usage, direct SQL UPDATE
/// </summary>
public static class UpdateProductStockHandler
{
    public static async Task<IResult> Handle(
        int categoryId,
        int stockAdjustment,
        AppDbContext db)
    {
        var affectedRows = await db.Products
            .Where(p => p.CategoryId == categoryId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Stock, p => p.Stock + stockAdjustment)
                .SetProperty(p => p.UpdatedDate, DateTime.UtcNow));

        return Results.Ok(new
        {
            AffectedRows = affectedRows,
            Message = $"Updated stock for {affectedRows} products in category {categoryId}"
        });
    }
}
