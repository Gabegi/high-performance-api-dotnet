namespace ApexShop.API.Endpoints.Reviews;

/// <summary>
/// Base orchestrator for all Review endpoints.
/// Delegates to specialized endpoint classes for each HTTP verb.
/// </summary>
public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reviews").WithTags("Reviews");

        // GET endpoints
        group.MapGetReviews();
        group.MapGetReviewById();

        // POST endpoints
        group.MapCreateReview();

        // PUT endpoints
        group.MapUpdateReview();

        // DELETE endpoints
        group.MapDeleteReview();
    }
}
