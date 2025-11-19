namespace ApexShop.API.Endpoints.Orders;

/// <summary>
/// Base orchestrator for all Order endpoints.
/// Delegates to specialized endpoint classes for each HTTP verb.
/// </summary>
public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders").WithTags("Orders");

        // GET endpoints
        group.MapGetOrders();
        group.MapGetOrderById();

        // POST endpoints
        group.MapCreateOrder();

        // PUT endpoints
        group.MapUpdateOrder();

        // DELETE endpoints
        group.MapDeleteOrder();
    }
}
