using ApexShop.LoadTests.Configuration;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace ApexShop.LoadTests.Load;

public class RealisticScenarios
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = LoadTestConfig.RequestTimeout,
        MaxResponseContentBufferSize = LoadTestConfig.MaxResponseBufferSize
    };

    static RealisticScenarios()
    {
        _httpClient.DefaultRequestHeaders.ConnectionClose = false;
    }

    public ScenarioProps BrowseAndAddReview()
    {
        var scenario = Scenario.Create("browse_and_review", async context =>
        {
            // Step 1: Browse products
            var getProductsRequest = Http.CreateRequest("GET", $"{LoadTestConfig.BaseUrl}/products")
                .WithHeader("Accept", "application/json");

            var productsResponse = await Http.Send(_httpClient, getProductsRequest);

            if (productsResponse.StatusCode != 200)
                return Response.Fail(
                    statusCode: productsResponse.StatusCode.ToString(),
                    error: $"Step 1 failed: Expected 200, got {productsResponse.StatusCode}"
                );

            // Step 2: Get specific product details
            var productId = Random.Shared.Next(1, LoadTestConfig.DataRanges.MaxProductId + 1);
            var getProductRequest = Http.CreateRequest("GET", $"{LoadTestConfig.BaseUrl}/products/{productId}")
                .WithHeader("Accept", "application/json");

            var productResponse = await Http.Send(_httpClient, getProductRequest);

            if (productResponse.StatusCode != 200)
                return Response.Fail(
                    statusCode: productResponse.StatusCode.ToString(),
                    error: $"Step 2 failed: Expected 200, got {productResponse.StatusCode}"
                );

            // Step 3: Add a review
            var uniqueComment = $"Great product! Review-{Guid.NewGuid().ToString("N")[..8]}";
            var review = $$"""
            {
                "productId": {{productId}},
                "userId": {{Random.Shared.Next(1, LoadTestConfig.DataRanges.MaxUserId + 1)}},
                "rating": {{Random.Shared.Next(1, 6)}},
                "comment": "{{uniqueComment}}",
                "isVerifiedPurchase": true
            }
            """;

            var reviewRequest = Http.CreateRequest("POST", $"{LoadTestConfig.BaseUrl}/reviews")
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Accept", "application/json")
                .WithBody(new StringContent(review, System.Text.Encoding.UTF8, "application/json"));

            var reviewResponse = await Http.Send(_httpClient, reviewRequest);

            if (reviewResponse.StatusCode != 201)
                return Response.Fail(
                    statusCode: reviewResponse.StatusCode.ToString(),
                    error: $"Step 3 failed: Expected 201, got {reviewResponse.StatusCode}"
                );

            return reviewResponse;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 5, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60))
        );

        return scenario;
    }

    public ScenarioProps CreateOrderWorkflow()
    {
        var scenario = Scenario.Create("create_order_workflow", async context =>
        {
            // Step 1: Browse categories
            var getCategoriesRequest = Http.CreateRequest("GET", $"{LoadTestConfig.BaseUrl}/categories")
                .WithHeader("Accept", "application/json");

            var categoriesResponse = await Http.Send(_httpClient, getCategoriesRequest);

            if (categoriesResponse.StatusCode != 200)
                return Response.Fail(
                    statusCode: categoriesResponse.StatusCode.ToString(),
                    error: $"Step 1 failed: Expected 200, got {categoriesResponse.StatusCode}"
                );

            // Step 2: Browse products
            var getProductsRequest = Http.CreateRequest("GET", $"{LoadTestConfig.BaseUrl}/products")
                .WithHeader("Accept", "application/json");

            var productsResponse = await Http.Send(_httpClient, getProductsRequest);

            if (productsResponse.StatusCode != 200)
                return Response.Fail(
                    statusCode: productsResponse.StatusCode.ToString(),
                    error: $"Step 2 failed: Expected 200, got {productsResponse.StatusCode}"
                );

            // Step 3: Create an order
            var userId = Random.Shared.Next(1, LoadTestConfig.DataRanges.MaxUserId + 1);
            var order = $$"""
            {
                "userId": {{userId}},
                "totalAmount": 299.99,
                "status": "Pending",
                "shippingAddress": "123 Test Street, City, Country"
            }
            """;

            var createOrderRequest = Http.CreateRequest("POST", $"{LoadTestConfig.BaseUrl}/orders")
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Accept", "application/json")
                .WithBody(new StringContent(order, System.Text.Encoding.UTF8, "application/json"));

            var orderResponse = await Http.Send(_httpClient, createOrderRequest);

            if (orderResponse.StatusCode != 201)
                return Response.Fail(
                    statusCode: orderResponse.StatusCode.ToString(),
                    error: $"Step 3 failed: Expected 201, got {orderResponse.StatusCode}"
                );

            return orderResponse;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 3, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60))
        );

        return scenario;
    }

    public ScenarioProps UserRegistrationAndBrowse()
    {
        var scenario = Scenario.Create("user_registration_and_browse", async context =>
        {
            // Step 1: Create user
            var uniqueEmail = $"user-{Guid.NewGuid():N}@test.com";
            var user = $$"""
            {
                "email": "{{uniqueEmail}}",
                "passwordHash": "hashedpassword123",
                "firstName": "Test",
                "lastName": "User"
            }
            """;

            var createUserRequest = Http.CreateRequest("POST", $"{LoadTestConfig.BaseUrl}/users")
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Accept", "application/json")
                .WithBody(new StringContent(user, System.Text.Encoding.UTF8, "application/json"));

            var userResponse = await Http.Send(_httpClient, createUserRequest);

            if (userResponse.StatusCode != 201)
                return Response.Fail(
                    statusCode: userResponse.StatusCode.ToString(),
                    error: $"Step 1 failed: Expected 201, got {userResponse.StatusCode}"
                );

            // Step 2: Browse categories
            var getCategoriesRequest = Http.CreateRequest("GET", $"{LoadTestConfig.BaseUrl}/categories")
                .WithHeader("Accept", "application/json");

            var categoriesResponse = await Http.Send(_httpClient, getCategoriesRequest);

            if (categoriesResponse.StatusCode != 200)
                return Response.Fail(
                    statusCode: categoriesResponse.StatusCode.ToString(),
                    error: $"Step 2 failed: Expected 200, got {categoriesResponse.StatusCode}"
                );

            // Step 3: Browse products
            var getProductsRequest = Http.CreateRequest("GET", $"{LoadTestConfig.BaseUrl}/products")
                .WithHeader("Accept", "application/json");

            var productsResponse = await Http.Send(_httpClient, getProductsRequest);

            if (productsResponse.StatusCode != 200)
                return Response.Fail(
                    statusCode: productsResponse.StatusCode.ToString(),
                    error: $"Step 3 failed: Expected 200, got {productsResponse.StatusCode}"
                );

            return productsResponse;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 2, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60))
        );

        return scenario;
    }
}