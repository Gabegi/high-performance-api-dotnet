using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace ApexShop.Benchmarks.Load;

public class RealisticScenarios
{
    private const string BaseUrl = "https://localhost:7001";

    public static ScenarioProps BrowseAndAddReview()
    {
        var httpFactory = Http.ClientFactory().CreateHttp();

        var scenario = Scenario.Create("browse_and_review", async context =>
        {
            // Step 1: Browse products
            var getProductsRequest = Http.CreateRequest("GET", $"{BaseUrl}/products")
                .WithHeader("Accept", "application/json");

            var productsResponse = await Http.Send(httpFactory, getProductsRequest);

            if (productsResponse.IsError)
                return productsResponse;

            // Step 2: Get specific product details
            var productId = Random.Shared.Next(1, 50);
            var getProductRequest = Http.CreateRequest("GET", $"{BaseUrl}/products/{productId}")
                .WithHeader("Accept", "application/json");

            var productResponse = await Http.Send(httpFactory, getProductRequest);

            if (productResponse.IsError)
                return productResponse;

            // Step 3: Add a review
            var review = $$"""
            {
                "productId": {{productId}},
                "userId": {{Random.Shared.Next(1, 20)}},
                "rating": {{Random.Shared.Next(1, 6)}},
                "comment": "Great product!",
                "isVerifiedPurchase": true
            }
            """;

            var reviewRequest = Http.CreateRequest("POST", $"{BaseUrl}/reviews")
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Accept", "application/json")
                .WithBody(new StringContent(review));

            var reviewResponse = await Http.Send(httpFactory, reviewRequest);
            return reviewResponse;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 20, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60))
        );

        return scenario;
    }

    public static ScenarioProps CreateOrderWorkflow()
    {
        var httpFactory = Http.ClientFactory().CreateHttp();

        var scenario = Scenario.Create("create_order_workflow", async context =>
        {
            // Step 1: Browse categories
            var getCategoriesRequest = Http.CreateRequest("GET", $"{BaseUrl}/categories")
                .WithHeader("Accept", "application/json");

            var categoriesResponse = await Http.Send(httpFactory, getCategoriesRequest);

            if (categoriesResponse.IsError)
                return categoriesResponse;

            // Step 2: Browse products
            var getProductsRequest = Http.CreateRequest("GET", $"{BaseUrl}/products")
                .WithHeader("Accept", "application/json");

            var productsResponse = await Http.Send(httpFactory, getProductsRequest);

            if (productsResponse.IsError)
                return productsResponse;

            // Step 3: Create an order
            var userId = Random.Shared.Next(1, 20);
            var order = $$"""
            {
                "userId": {{userId}},
                "totalAmount": 299.99,
                "status": "Pending",
                "shippingAddress": "123 Test Street, City, Country"
            }
            """;

            var createOrderRequest = Http.CreateRequest("POST", $"{BaseUrl}/orders")
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Accept", "application/json")
                .WithBody(new StringContent(order));

            var orderResponse = await Http.Send(httpFactory, createOrderRequest);
            return orderResponse;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 15, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60))
        );

        return scenario;
    }

    public static ScenarioProps UserRegistrationAndBrowse()
    {
        var httpFactory = Http.ClientFactory().CreateHttp();

        var scenario = Scenario.Create("user_registration_and_browse", async context =>
        {
            // Step 1: Create user
            var user = $$"""
            {
                "email": "user{{Random.Shared.Next(1000, 9999)}}@test.com",
                "passwordHash": "hashedpassword123",
                "firstName": "Test",
                "lastName": "User"
            }
            """;

            var createUserRequest = Http.CreateRequest("POST", $"{BaseUrl}/users")
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Accept", "application/json")
                .WithBody(new StringContent(user));

            var userResponse = await Http.Send(httpFactory, createUserRequest);

            if (userResponse.IsError)
                return userResponse;

            // Step 2: Browse categories
            var getCategoriesRequest = Http.CreateRequest("GET", $"{BaseUrl}/categories")
                .WithHeader("Accept", "application/json");

            var categoriesResponse = await Http.Send(httpFactory, getCategoriesRequest);

            if (categoriesResponse.IsError)
                return categoriesResponse;

            // Step 3: Browse products
            var getProductsRequest = Http.CreateRequest("GET", $"{BaseUrl}/products")
                .WithHeader("Accept", "application/json");

            var productsResponse = await Http.Send(httpFactory, getProductsRequest);
            return productsResponse;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 10, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60))
        );

        return scenario;
    }
}
