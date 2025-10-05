using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace ApexShop.LoadTests.Load;

public class StressScenarios
{
    private const string BaseUrl = "http://localhost:5193";
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
        MaxResponseContentBufferSize = 10_000_000 // 10MB
    };

    static StressScenarios()
    {
        _httpClient.DefaultRequestHeaders.ConnectionClose = false;
    }

    public ScenarioProps HighLoadGetProducts()
    {
        var scenario = Scenario.Create("stress_get_products", async context =>
        {
            var request = Http.CreateRequest("GET", $"{BaseUrl}/products")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(_httpClient, request);
            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(10))
        .WithLoadSimulations(
            // Ramp up to 50 RPS over 30 seconds
            Simulation.RampingInject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)),
            // Sustain 50 RPS for 60 seconds
            Simulation.Inject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60)),
            // Ramp down
            Simulation.RampingInject(rate: 0, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        return scenario;
    }

    public ScenarioProps SpikeTest()
    {
        var scenario = Scenario.Create("spike_test", async context =>
        {
            var request = Http.CreateRequest("GET", $"{BaseUrl}/products")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(_httpClient, request);
            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            // Normal load
            Simulation.Inject(rate: 10, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)),
            // Sudden spike
            Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)),
            // Back to normal
            Simulation.Inject(rate: 10, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
        );

        return scenario;
    }

    public ScenarioProps ConstantLoad()
    {
        var scenario = Scenario.Create("constant_load", async context =>
        {
            var productId = Random.Shared.Next(1, 15001); // Match actual product count
            var request = Http.CreateRequest("GET", $"{BaseUrl}/products/{productId}")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(_httpClient, request);
            return response;
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            // Keep constant load with 10 concurrent users for 1 minute
            Simulation.KeepConstant(copies: 10, during: TimeSpan.FromMinutes(1))
        );

        return scenario;
    }

    public ScenarioProps MixedOperationsStress()
    {
        var scenario = Scenario.Create("mixed_operations_stress", async context =>
        {
            var operation = Random.Shared.Next(0, 4);

            if (operation == 0)
            {
                var request = Http.CreateRequest("GET", $"{BaseUrl}/products")
                    .WithHeader("Accept", "application/json");
                return await Http.Send(_httpClient, request);
            }
            else if (operation == 1)
            {
                var productId = Random.Shared.Next(1, 15001); // Match actual product count
                var request = Http.CreateRequest("GET", $"{BaseUrl}/products/{productId}")
                    .WithHeader("Accept", "application/json");
                return await Http.Send(_httpClient, request);
            }
            else if (operation == 2)
            {
                var product = $$"""
            {
                "name": "Stress Test Product {{Random.Shared.Next(1000, 9999)}}",
                "description": "Stress test product",
                "price": 49.99,
                "stock": 50,
                "categoryId": {{Random.Shared.Next(1, 16)}}
            }
            """;

                var request = Http.CreateRequest("POST", $"{BaseUrl}/products")
                    .WithHeader("Content-Type", "application/json")
                    .WithHeader("Accept", "application/json")
                    .WithBody(new StringContent(product, System.Text.Encoding.UTF8, "application/json"));

                return await Http.Send(_httpClient, request);
            }
            else
            {
                var request = Http.CreateRequest("GET", $"{BaseUrl}/categories")
                    .WithHeader("Accept", "application/json");
                return await Http.Send(_httpClient, request);
            }
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(10))
        .WithLoadSimulations(
            Simulation.RampingInject(rate: 30, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60))
        );

        return scenario;
    }
}