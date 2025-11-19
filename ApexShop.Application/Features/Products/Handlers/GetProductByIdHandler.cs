using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Queries;
using ApexShop.Application.DTOs;

namespace ApexShop.Application.Features.Products.Handlers;

/// <summary>
/// Handler for GET /products/{id} - Retrieve a single product by ID
/// Uses compiled queries for optimal performance.
/// </summary>
public static class GetProductByIdHandler
{
    public static async Task<IResult> Handle(
        int id,
        AppDbContext db)
    {
        var product = await CompiledQueries.GetProductById(db, id);
        if (product is null)
            return Results.NotFound();

        return Results.Ok(new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.Stock,
            product.CategoryId,
            product.CreatedDate,
            product.UpdatedDate));
    }
}
