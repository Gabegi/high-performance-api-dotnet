using ApexShop.Infrastructure.Entities;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Diagnostics.Windows;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;

namespace ApexShop.Benchmarks.Micro;

[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
[ThreadingDiagnoser]
[ExceptionDiagnoser]
// EventPipeProfiler shows time breakdown: HTTP transport, JSON serialization, DB queries, middleware
// NOTE: Adds ~10% overhead but provides valuable insights into where time is spent
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
// NOTE: HardwareCounters requires Administrator privileges on Windows
// Run Visual Studio/Terminal as Admin or comment out this attribute
// Useful for cache optimization analysis (CacheMisses, LlcMisses)
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
    private HttpClient? _client;
    private WebApplicationFactory<ApexShop.API.Program>? _factory;

    // =============================================================================
    // GLOBAL SETUP
    // =============================================================================
    [GlobalSetup]
    public void Setup()
    {
        _factory = new WebApplicationFactory<ApexShop.API.Program>();
        _client = _factory.CreateClient();
    }

    // =============================================================================
    // COLD START TESTS
    // =============================================================================
    [Benchmark]
    public async Task<Product?> Api_ColdStart()
    {
        using var factory = new WebApplicationFactory<ApexShop.API.Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/products/1");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Product>();
    }

    [Benchmark]
    [IterationCount(1)]
    [WarmupCount(0)]
    public async Task<Product?> Api_TrueColdStart()
    {
        using var factory = new WebApplicationFactory<ApexShop.API.Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/products/1");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Product>();
    }

    // =============================================================================
    // SINGLE REQUEST TESTS
    // =============================================================================
    [Benchmark(Baseline = true)]
    public async Task<Product?> Api_GetSingleProduct()
    {
        var productId = Random.Shared.Next(1, 15001); // Match actual product count
        var response = await _client!.GetAsync($"/products/{productId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Product>();
    }

    // =============================================================================
    // LIST TESTS
    // =============================================================================
    [Benchmark]
    public async Task<List<Product>?> Api_GetAllProducts()
    {
        var response = await _client!.GetAsync("/products");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Product>>();
    }

    // =============================================================================
    // CLEANUP
    // =============================================================================
    [GlobalCleanup]
    public void Cleanup()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    // =============================================================================
    // CONFIG
    // =============================================================================
    public class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            // Get solution root and place reports in ApexShop.Benchmarks.Micro/Reports with timestamp
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var reportsDir = Path.Combine(
                GetSolutionRoot() ?? AppContext.BaseDirectory,
                "ApexShop.Benchmarks.Micro",
                "Reports",
                timestamp
            );
            ArtifactsPath = Path.GetFullPath(reportsDir);

            // Export formats - using explicit methods instead of AddExporter to avoid duplicates
            AddExporter(BenchmarkDotNet.Exporters.Csv.CsvExporter.Default);
            AddExporter(BenchmarkDotNet.Exporters.HtmlExporter.Default);
        }

        static string? GetSolutionRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (directory.GetFiles("*.sln").Length > 0)
                    return directory.FullName;
                directory = directory.Parent;
            }
            return null;
        }
    }
}
