namespace ApexShop.API.Endpoints.Products;

/// <summary>
/// Base orchestrator for all Product endpoints.
/// Delegates to specialized endpoint classes for each HTTP verb.
/// </summary>
public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products").WithTags("Products");

        // GET endpoints
        group.MapGetProducts();
        group.MapGetProductById();

        // POST endpoints
        group.MapCreateProduct();

        // PUT endpoints
        group.MapUpdateProduct();

        // DELETE endpoints
        group.MapDeleteProduct();

    }
}
