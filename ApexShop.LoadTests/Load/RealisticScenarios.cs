using ApexShop.LoadTests.Configuration;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace ApexShop.LoadTests.Load;

/// <summary>
/// Removed: All realistic scenario workflows involved non-Products endpoints.
/// Load testing focused on Products endpoint only.
/// </summary>
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
}