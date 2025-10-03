using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http;
using NBomber.Http.CSharp;

namespace ApexShop.Benchmarks.Load;

public class StressScenarios
{
    private const string BaseUrl = "https://localhost:7001";

    public static ScenarioProps HighLoadGetProducts()
    {
        var httpFactory = Http.ClientFactory().CreateHttp();

        var scenario = Scenario.Create("stress_get_products", async context =>
        {
            var request = Http.CreateRequest("GET", $"{BaseUrl}/products")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(httpFactory, request);
            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(10))
        .WithLoadSimulations(
            // Ramp up to 500 RPS over 30 seconds
            Simulation.RampingInject(rate: 500, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)),
            // Sustain 500 RPS for 60 seconds
            Simulation.Inject(rate: 500, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60)),
            // Ramp down
            Simulation.RampingInject(rate: 0, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        return scenario;
    }

    public static ScenarioProps SpikeTest()
    {
        var httpFactory = Http.ClientFactory().CreateHttp();

        var scenario = Scenario.Create("spike_test", async context =>
        {
            var request = Http.CreateRequest("GET", $"{BaseUrl}/products")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(httpFactory, request);
            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            // Normal load
            Simulation.Inject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)),
            // Sudden spike
            Simulation.Inject(rate: 1000, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)),
            // Back to normal
            Simulation.Inject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        return scenario;
    }

    public static ScenarioProps ConstantLoad()
    {
        var httpFactory = Http.ClientFactory().CreateHttp();

        var scenario = Scenario.Create("constant_load", async context =>
        {
            var productId = Random.Shared.Next(1, 100);
            var request = Http.CreateRequest("GET", $"{BaseUrl}/products/{productId}")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(httpFactory, request);
            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            // Keep constant load with 50 concurrent users for 2 minutes
            Simulation.KeepConstant(copies: 50, during: TimeSpan.FromMinutes(2))
        );

        return scenario;
    }

    public static ScenarioProps MixedOperationsStress()
    {
        var httpFactory = Http.ClientFactory().CreateHttp();

        var scenario = Scenario.Create("mixed_operations_stress", async context =>
        {
            var operation = Random.Shared.Next(0, 4);

            Response response = operation switch
            {
                0 => await GetProducts(),
                1 => await GetProductById(),
                2 => await CreateProduct(),
                _ => await GetCategories()
            };

            return response;

            async Task<Response> GetProducts()
            {
                var request = Http.CreateRequest("GET", $"{BaseUrl}/products")
                    .WithHeader("Accept", "application/json");
                return await Http.Send(httpFactory, request);
            }

            async Task<Response> GetProductById()
            {
                var productId = Random.Shared.Next(1, 100);
                var request = Http.CreateRequest("GET", $"{BaseUrl}/products/{productId}")
                    .WithHeader("Accept", "application/json");
                return await Http.Send(httpFactory, request);
            }

            async Task<Response> CreateProduct()
            {
                var product = $$"""
                {
                    "name": "Stress Test Product {{Random.Shared.Next(1000, 9999)}}",
                    "description": "Stress test product",
                    "price": 49.99,
                    "stock": 50,
                    "categoryId": 1
                }
                """;

                var request = Http.CreateRequest("POST", $"{BaseUrl}/products")
                    .WithHeader("Content-Type", "application/json")
                    .WithHeader("Accept", "application/json")
                    .WithBody(new StringContent(product));

                return await Http.Send(httpFactory, request);
            }

            async Task<Response> GetCategories()
            {
                var request = Http.CreateRequest("GET", $"{BaseUrl}/categories")
                    .WithHeader("Accept", "application/json");
                return await Http.Send(httpFactory, request);
            }
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(10))
        .WithLoadSimulations(
            Simulation.RampingInject(rate: 300, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60))
        );

        return scenario;
    }
}
