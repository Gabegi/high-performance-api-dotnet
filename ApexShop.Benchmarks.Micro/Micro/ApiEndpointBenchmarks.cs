using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using System.Net.Http.Json;
using BenchmarkDotNet.Diagnostics.Windows; // ensures assembly is copied

namespace ApexShop.Benchmarks.Micro;

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
[ThreadingDiagnoser]
[ExceptionDiagnoser]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
[HardwareCounters(
    HardwareCounter.BranchMispredictions,
    HardwareCounter.CacheMisses,
    HardwareCounter.TotalCycles,
    HardwareCounter.TotalIssues,
    HardwareCounter.BranchInstructions,
    HardwareCounter.LlcMisses)]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(RuntimeMoniker.Net90)]
[GcServer(true)]
[WarmupCount(5)]
[IterationCount(15)]
public class ApiEndpointBenchmarks
{
    private HttpClient _httpClient = null!;
    private const string BaseUrl = "https://localhost:7001";

    [GlobalSetup]
    public void Setup()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        _httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(BaseUrl)
        };
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _httpClient.Dispose();
    }

    [Benchmark]
    public async Task<int> GetProducts()
    {
        var response = await _httpClient.GetAsync("/products");
        var content = await response.Content.ReadAsStringAsync();
        return content.Length;
    }

    [Benchmark]
    public async Task<int> GetProductById()
    {
        var response = await _httpClient.GetAsync("/products/1");
        var content = await response.Content.ReadAsStringAsync();
        return content.Length;
    }

    [Benchmark]
    public async Task<int> GetCategories()
    {
        var response = await _httpClient.GetAsync("/categories");
        var content = await response.Content.ReadAsStringAsync();
        return content.Length;
    }

    [Benchmark]
    public async Task<int> GetOrders()
    {
        var response = await _httpClient.GetAsync("/orders");
        var content = await response.Content.ReadAsStringAsync();
        return content.Length;
    }

    [Benchmark]
    public async Task<int> CreateProduct()
    {
        var product = new
        {
            name = $"Benchmark Product {Random.Shared.Next(1000, 9999)}",
            description = "Benchmark test product",
            price = 99.99m,
            stock = 100,
            categoryId = 1
        };

        var response = await _httpClient.PostAsJsonAsync("/products", product);
        var content = await response.Content.ReadAsStringAsync();
        return content.Length;
    }

    [Benchmark]
    public async Task<int> GetUsers()
    {
        var response = await _httpClient.GetAsync("/users");
        var content = await response.Content.ReadAsStringAsync();
        return content.Length;
    }

    public class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            // Save benchmark results inside Results folder
            ArtifactsPath = Path.Combine(AppContext.BaseDirectory, "Results");
        }
    }
}


