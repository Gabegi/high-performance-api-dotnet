using BenchmarkDotNet.Attributes;
using System.Net.Http.Json;

namespace ApexShop.Benchmarks.Micro;

[MemoryDiagnoser]
[GcServer(true)]
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
}
