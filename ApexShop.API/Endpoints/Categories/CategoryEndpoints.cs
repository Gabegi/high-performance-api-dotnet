namespace ApexShop.API.Endpoints.Categories;

/// <summary>
/// Base orchestrator for all Category endpoints.
/// Delegates to specialized endpoint classes for each HTTP verb.
/// </summary>
public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/categories").WithTags("Categories");

        // GET endpoints
        group.MapGetCategories();
        group.MapGetCategoryById();

        // POST endpoints
        group.MapCreateCategory();

        // PUT endpoints
        group.MapUpdateCategory();

        // DELETE endpoints
        group.MapDeleteCategory();
    }
}
