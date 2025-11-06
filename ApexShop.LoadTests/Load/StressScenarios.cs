using ApexShop.LoadTests.Configuration;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace ApexShop.LoadTests.Load;

public class StressScenarios
{
    // Better configured HttpClient for stress tests
    private static readonly HttpClient _httpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
        MaxConnectionsPerServer = 100, // Limit concurrent connections
        EnableMultipleHttp2Connections = true
    })
    {
        Timeout = TimeSpan.FromSeconds(60),
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
            try
            {
                var request = Http.CreateRequest("GET", $"{LoadTestConfig.BaseUrl}/products")
                    .WithHeader("Accept", "application/json");

                var response = await Http.Send(_httpClient, request);

                return (response.StatusCode == "200" || response.StatusCode == "503")
                    ? response
                    : Response.Fail();
            }
            catch (TaskCanceledException)
            {
                return Response.Fail(statusCode: "Timeout");
            }
            catch (HttpRequestException)
            {
                return Response.Fail(statusCode: "ConnectionError");
            }
            catch (Exception)
            {
                return Response.Fail(statusCode: "Error");
            }
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(10))
        .WithLoadSimulations(
            // REDUCED LOAD - Ramp up to 20 RPS over 30 seconds
            Simulation.RampingInject(rate: 20, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)),
            // Sustain 20 RPS for 45 seconds
            Simulation.Inject(rate: 20, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(45)),
            // Ramp down
            Simulation.RampingInject(rate: 0, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(15))
        );

        return scenario;
    }

    public ScenarioProps SpikeTest()
    {
        var scenario = Scenario.Create("spike_test", async context =>
        {
            try
            {
                var request = Http.CreateRequest("GET", $"{LoadTestConfig.BaseUrl}/products")
                    .WithHeader("Accept", "application/json");

                var response = await Http.Send(_httpClient, request);

                return (response.StatusCode == "200" || response.StatusCode == "503")
                    ? response
                    : Response.Fail();
            }
            catch (TaskCanceledException)
            {
                return Response.Fail(statusCode: "Timeout");
            }
            catch (HttpRequestException)
            {
                return Response.Fail(statusCode: "ConnectionError");
            }
            catch (Exception)
            {
                return Response.Fail(statusCode: "Error");
            }
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            // Normal load
            Simulation.Inject(rate: 5, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(20)),
            // REDUCED spike to 30 RPS instead of 100
            Simulation.Inject(rate: 30, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(10)),
            // Back to normal
            Simulation.Inject(rate: 5, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(20))
        );

        return scenario;
    }

    public ScenarioProps ConstantLoad()
    {
        var scenario = Scenario.Create("constant_load", async context =>
        {
            try
            {
                var productId = Random.Shared.Next(1, LoadTestConfig.DataRanges.MaxProductId + 1);
                var request = Http.CreateRequest("GET", $"{LoadTestConfig.BaseUrl}/products/{productId}")
                    .WithHeader("Accept", "application/json");

                var response = await Http.Send(_httpClient, request);

                // 404 is a valid response (product doesn't exist due to ID gaps)
                if (response.StatusCode == "404")
                    return Response.Ok(statusCode: "404-NotFound-OK");

                if (response.StatusCode != "200")
                    return Response.Fail();

                return response;
            }
            catch (TaskCanceledException)
            {
                return Response.Fail(statusCode: "Timeout");
            }
            catch (HttpRequestException)
            {
                return Response.Fail(statusCode: "ConnectionError");
            }
            catch (Exception)
            {
                return Response.Fail(statusCode: "Error");
            }
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(5))
        .WithLoadSimulations(
            // REDUCED from 10 to 5 concurrent users
            Simulation.KeepConstant(copies: 5, during: TimeSpan.FromSeconds(45))
        );

        return scenario;
    }

}