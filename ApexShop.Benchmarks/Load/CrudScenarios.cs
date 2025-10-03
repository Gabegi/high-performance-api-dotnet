using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace ApexShop.Benchmarks.Load;

public class CrudScenarios
{
    private const string BaseUrl = "https://localhost:7001";

    public static ScenarioProps GetProducts()
    {
        var scenario = Scenario.Create("get_products", async context =>
        {
            var request = Http.CreateRequest("GET", $"{BaseUrl}/products")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(context.Client, request);
            return response;
        })
        .WithInit(context => Task.FromResult(Http.ClientFactory().CreateHttp()))
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        return scenario;
    }

    public static ScenarioProps GetProductById()
    {
        var httpFactory = Http.ClientFactory().CreateHttp();

        var scenario = Scenario.Create("get_product_by_id", async context =>
        {
            var productId = Random.Shared.Next(1, 100);
            var request = Http.CreateRequest("GET", $"{BaseUrl}/products/{productId}")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(httpFactory, request);
            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        return scenario;
    }

    public static ScenarioProps CreateProduct()
    {
        var httpFactory = Http.ClientFactory().CreateHttp();

        var scenario = Scenario.Create("create_product", async context =>
        {
            var product = $$"""
            {
                "name": "Test Product {{Random.Shared.Next(1000, 9999)}}",
                "description": "Benchmark test product",
                "price": 99.99,
                "stock": 100,
                "categoryId": 1
            }
            """;

            var request = Http.CreateRequest("POST", $"{BaseUrl}/products")
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Accept", "application/json")
                .WithBody(new StringContent(product));

            var response = await Http.Send(httpFactory, request);
            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        return scenario;
    }

    public static ScenarioProps GetCategories()
    {
        var httpFactory = Http.ClientFactory().CreateHttp();

        var scenario = Scenario.Create("get_categories", async context =>
        {
            var request = Http.CreateRequest("GET", $"{BaseUrl}/categories")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(httpFactory, request);
            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        return scenario;
    }

    public static ScenarioProps GetOrders()
    {
        var httpFactory = Http.ClientFactory().CreateHttp();

        var scenario = Scenario.Create("get_orders", async context =>
        {
            var request = Http.CreateRequest("GET", $"{BaseUrl}/orders")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(httpFactory, request);
            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        return scenario;
    }
}
