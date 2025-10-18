using ApexShop.LoadTests.Configuration;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace ApexShop.LoadTests.Load;

public class StressScenarios
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = LoadTestConfig.RequestTimeout,
        MaxResponseContentBufferSize = LoadTestConfig.MaxResponseBufferSize
    };

    static StressScenarios()
    {
        _httpClient.DefaultRequestHeaders.ConnectionClose = false;
    }

    public ScenarioProps HighLoadGetProducts()
    {
        var scenario = Scenario.Create("stress_get_products", async context =>
        {
            var request = Http.CreateRequest("GET", $"{LoadTestConfig.BaseUrl}/products")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(_httpClient, request);

            // For stress tests, we accept both success and service unavailable
            return (response.StatusCode == "200" || response.StatusCode == "503")
                ? response
                : Response.Fail();
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
            var request = Http.CreateRequest("GET", $"{LoadTestConfig.BaseUrl}/products")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(_httpClient, request);

            // For stress tests, we accept both success and service unavailable
            return (response.StatusCode == "200" || response.StatusCode == "503")
                ? response
                : Response.Fail();
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
            var productId = Random.Shared.Next(1, LoadTestConfig.DataRanges.MaxProductId + 1);
            var request = Http.CreateRequest("GET", $"{LoadTestConfig.BaseUrl}/products/{productId}")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(_httpClient, request);

            // Validate response
            return response.IsError
                ? Response.Fail()
                : response;
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
                var request = Http.CreateRequest("GET", $"{LoadTestConfig.BaseUrl}/products")
                    .WithHeader("Accept", "application/json");
                var response = await Http.Send(_httpClient, request);

                if (response.StatusCode != "200" && response.StatusCode != "503")
                    return Response.Fail();

                return response;
            }
            else if (operation == 1)
            {
                var productId = Random.Shared.Next(1, LoadTestConfig.DataRanges.MaxProductId + 1);
                var request = Http.CreateRequest("GET", $"{LoadTestConfig.BaseUrl}/products/{productId}")
                    .WithHeader("Accept", "application/json");
                var response = await Http.Send(_httpClient, request);

                if (response.IsError)
                    return Response.Fail();

                return response;
            }
            else if (operation == 2)
            {
                var uniqueId = Guid.NewGuid().ToString("N")[..8];
                var product = $$"""
            {
                "name": "Stress Test Product {{uniqueId}}",
                "description": "Stress test product",
                "price": 49.99,
                "stock": 50,
                "categoryId": {{Random.Shared.Next(1, LoadTestConfig.DataRanges.MaxCategoryId + 1)}}
            }
            """;

                var request = Http.CreateRequest("POST", $"{LoadTestConfig.BaseUrl}/products")
                    .WithHeader("Content-Type", "application/json")
                    .WithHeader("Accept", "application/json")
                    .WithBody(new StringContent(product, System.Text.Encoding.UTF8, "application/json"));

                var response = await Http.Send(_httpClient, request);

                if (response.IsError)
                    return Response.Fail();

                return response;
            }
            else
            {
                var request = Http.CreateRequest("GET", $"{LoadTestConfig.BaseUrl}/categories")
                    .WithHeader("Accept", "application/json");
                var response = await Http.Send(_httpClient, request);

                if (response.IsError)
                    return Response.Fail();

                return response;
            }
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(10))
        .WithLoadSimulations(
            Simulation.RampingInject(rate: 30, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60))
        );

        return scenario;
    }
}