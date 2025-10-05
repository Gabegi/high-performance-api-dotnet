using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace ApexShop.LoadTests.Load;

public class CrudScenarios
{
    private const string BaseUrl = "http://localhost:5193";
    private static readonly HttpClient _httpClient = new();

    public ScenarioProps GetProducts()
    {
        var scenario = Scenario.Create("get_products", async context =>
        {
            var request = Http.CreateRequest("GET", $"{BaseUrl}/products")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(_httpClient, request);
            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        return scenario;
    }

    public ScenarioProps GetProductById()
    {
        var scenario = Scenario.Create("get_product_by_id", async context =>
        {
            var productId = Random.Shared.Next(1, 15001); // Match actual product count
            var request = Http.CreateRequest("GET", $"{BaseUrl}/products/{productId}")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(_httpClient, request);
            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        return scenario;
    }

    public ScenarioProps CreateProduct()
    {
        var scenario = Scenario.Create("create_product", async context =>
        {
            var product = $$"""
            {
                "name": "Test Product {{Random.Shared.Next(1000, 9999)}}",
                "description": "Benchmark test product",
                "price": 99.99,
                "stock": 100,
                "categoryId": {{Random.Shared.Next(1, 16)}}
            }
            """;

            var request = Http.CreateRequest("POST", $"{BaseUrl}/products")
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Accept", "application/json")
                .WithBody(new StringContent(product));

            var response = await Http.Send(_httpClient, request);
            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        return scenario;
    }

    public ScenarioProps GetCategories()
    {
        var scenario = Scenario.Create("get_categories", async context =>
        {
            var request = Http.CreateRequest("GET", $"{BaseUrl}/categories")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(_httpClient, request);
            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        return scenario;
    }

    public ScenarioProps GetOrders()
    {
        var scenario = Scenario.Create("get_orders", async context =>
        {
            var request = Http.CreateRequest("GET", $"{BaseUrl}/orders")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(_httpClient, request);
            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        return scenario;
    }
}