using ApexShop.LoadTests.Configuration;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace ApexShop.LoadTests.Load;

public class CrudScenarios
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = LoadTestConfig.RequestTimeout,
        MaxResponseContentBufferSize = LoadTestConfig.MaxResponseBufferSize
    };

    static CrudScenarios()
    {
        _httpClient.DefaultRequestHeaders.ConnectionClose = false;
    }

    public ScenarioProps GetProducts()
    {
        var scenario = Scenario.Create("get_products", async context =>
        {
            var request = Http.CreateRequest("GET", $"{LoadTestConfig.BaseUrl}/products")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(_httpClient, request);

            // Validate response
            return response.IsError
                ? Response.Fail()
                : response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 10, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        return scenario;
    }

    public ScenarioProps GetProductById()
    {
        var scenario = Scenario.Create("get_product_by_id", async context =>
        {
            var productId = Random.Shared.Next(1, LoadTestConfig.DataRanges.MaxProductId + 1);
            var request = Http.CreateRequest("GET", $"{LoadTestConfig.BaseUrl}/products/{productId}")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(_httpClient, request);

            // 404 is a valid response (product doesn't exist due to ID gaps from benchmarks)
            if (response.StatusCode == "404")
                return Response.Ok(statusCode: "404-NotFound-OK");

            if (response.StatusCode != "200")
                return Response.Fail();

            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 10, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        return scenario;
    }

    public ScenarioProps CreateProduct()
    {
        var scenario = Scenario.Create("create_product", async context =>
        {
            var uniqueId = Guid.NewGuid().ToString("N")[..8]; // Use first 8 chars of GUID for uniqueness
            var product = $$"""
            {
                "name": "Test Product {{uniqueId}}",
                "description": "Benchmark test product",
                "price": 99.99,
                "stock": 100,
                "categoryId": {{Random.Shared.Next(1, LoadTestConfig.DataRanges.MaxCategoryId + 1)}}
            }
            """;

            // ✅ FIX: Dispose StringContent properly
            using var content = new StringContent(product, System.Text.Encoding.UTF8, "application/json");

            var request = Http.CreateRequest("POST", $"{LoadTestConfig.BaseUrl}/products")
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Accept", "application/json")
                .WithBody(content);

            var response = await Http.Send(_httpClient, request);

            // Validate response
            return response.IsError
                ? Response.Fail()
                : response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 5, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        return scenario;
    }

}