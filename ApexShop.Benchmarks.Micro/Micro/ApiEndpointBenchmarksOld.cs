using ApexShop.Infrastructure.Entities;
using ApexShop.API.DTOs;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Diagnostics.Windows;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

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
public class ApiEndpointBenchmarksOld
{
    private HttpClient? _client;
    private WebApplicationFactory<ApexShop.API.Program>? _factory;

    // =============================================================================
    // GLOBAL SETUP
    // =============================================================================
    [GlobalSetup]
    public void Setup()
    {
        _factory = new WebApplicationFactory<ApexShop.API.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production"); // Use Production mode to disable verbose logging
            });
        _client = _factory.CreateClient();
    }

    // =============================================================================
    // COLD START TESTS
    // =============================================================================
    [Benchmark]
    public async Task<Product?> Api_ColdStart()
    {
        using var factory = new WebApplicationFactory<ApexShop.API.Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
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
        using var factory = new WebApplicationFactory<ApexShop.API.Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
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
    // STREAMING TESTS - Constant memory regardless of dataset size
    // =============================================================================
    [Benchmark]
    public async Task<int> Api_StreamProducts_AllItems()
    {
        // Stream all products - measures true streaming performance
        var response = await _client!.GetAsync("/products/stream");
        response.EnsureSuccessStatusCode();

        int count = 0;
        await foreach (var product in response.Content.ReadFromJsonAsAsyncEnumerable<ProductListDto>())
        {
            if (product != null)
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark]
    public async Task<int> Api_StreamProducts_Limited1000()
    {
        // Stream limited items using category filter (first 1000 approx)
        var response = await _client!.GetAsync("/products/stream?categoryId=1");
        response.EnsureSuccessStatusCode();

        int count = 0;
        await foreach (var product in response.Content.ReadFromJsonAsAsyncEnumerable<ProductListDto>())
        {
            if (product != null)
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark]
    public async Task<int> Api_StreamOrders_AllItems()
    {
        var response = await _client!.GetAsync("/orders/stream");
        response.EnsureSuccessStatusCode();

        int count = 0;
        await foreach (var order in response.Content.ReadFromJsonAsAsyncEnumerable<OrderListDto>())
        {
            if (order != null)
            {
                count++;
            }
        }
        return count;
    }

    // =============================================================================
    // PAGINATION COMPARISON - Offset vs Cursor
    // =============================================================================
    [Benchmark]
    public async Task Api_OffsetPagination_Page1()
    {
        var response = await _client!.GetAsync("/products?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();
    }

    [Benchmark]
    public async Task Api_OffsetPagination_Page100()
    {
        // Deep pagination - slow with offset (O(n) where n = page * pageSize)
        // Skips 4,950 records
        var response = await _client!.GetAsync("/products?page=100&pageSize=50");
        response.EnsureSuccessStatusCode();
    }

    [Benchmark]
    public async Task Api_OffsetPagination_Page250()
    {
        // Very deep pagination - demonstrates O(n) scaling problem
        // Skips 12,450 records - clearly shows offset penalty
        var response = await _client!.GetAsync("/products?page=250&pageSize=50");
        response.EnsureSuccessStatusCode();
    }

    [Benchmark]
    public async Task Api_CursorPagination_First()
    {
        var response = await _client!.GetAsync("/products/cursor?pageSize=50");
        response.EnsureSuccessStatusCode();
    }

    [Benchmark]
    public async Task Api_CursorPagination_Deep()
    {
        // Simulate deep pagination with cursor (O(1) performance)
        var response = await _client!.GetAsync("/products/cursor?afterId=5000&pageSize=50");
        response.EnsureSuccessStatusCode();
    }

    [Benchmark]
    public async Task Api_CursorPagination_VeryDeep()
    {
        // Very deep pagination with cursor - still O(1) performance
        var response = await _client!.GetAsync("/products/cursor?afterId=12450&pageSize=50");
        response.EnsureSuccessStatusCode();
    }

    // =============================================================================
    // BULK OPERATIONS - AddRange, Streaming Updates, ExecuteDelete
    // =============================================================================
    [Benchmark]
    [Arguments(50)]
    [Arguments(100)]
    [Arguments(500)]
    public async Task Api_BulkCreate_NProducts(int count)
    {
        var products = Enumerable.Range(1, count).Select(i => new Product
        {
            Name = $"BenchProduct-{Guid.NewGuid()}",
            Description = "Benchmark test product",
            Price = 99.99m,
            Stock = 100,
            CategoryId = 1
        }).ToList();

        var json = JsonSerializer.Serialize(products);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client!.PostAsync("/products/bulk", content);
        response.EnsureSuccessStatusCode();

        // Cleanup: Delete created products to prevent data pollution
        var result = await response.Content.ReadFromJsonAsync<BulkCreateResult>();
        if (result?.ProductIds != null && result.ProductIds.Count > 0)
        {
            var deleteJson = JsonSerializer.Serialize(result.ProductIds);
            var deleteContent = new StringContent(deleteJson, Encoding.UTF8, "application/json");
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/products/bulk")
            {
                Content = deleteContent
            };
            await _client!.SendAsync(deleteRequest);
        }
    }

    [Benchmark]
    [Arguments(50)]
    [Arguments(100)]
    public async Task Api_BulkUpdate_NProducts(int count)
    {
        // First create test products
        var createProducts = Enumerable.Range(1, count).Select(i => new Product
        {
            Name = $"UpdateTest-{Guid.NewGuid()}",
            Description = "Update test",
            Price = 50m,
            Stock = 50,
            CategoryId = 1
        }).ToList();

        var createJson = JsonSerializer.Serialize(createProducts);
        var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");
        var createResponse = await _client!.PostAsync("/products/bulk", createContent);
        createResponse.EnsureSuccessStatusCode();

        var createdResult = await createResponse.Content.ReadFromJsonAsync<BulkCreateResult>();
        var productIds = createdResult?.ProductIds ?? new List<int>();

        // Now update them with streaming + batching
        var updateProducts = productIds.Select(id => new Product
        {
            Id = id,
            Name = $"Updated-{id}",
            Description = "Updated via bulk",
            Price = 75m,
            Stock = 75,
            CategoryId = 1
        }).ToList();

        var updateJson = JsonSerializer.Serialize(updateProducts);
        var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");
        var updateResponse = await _client!.PutAsync("/products/bulk", updateContent);
        updateResponse.EnsureSuccessStatusCode();

        // Cleanup: Delete created products
        if (productIds.Count > 0)
        {
            var deleteJson = JsonSerializer.Serialize(productIds);
            var deleteContent = new StringContent(deleteJson, Encoding.UTF8, "application/json");
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/products/bulk")
            {
                Content = deleteContent
            };
            await _client!.SendAsync(deleteRequest);
        }
    }

    [Benchmark]
    [Arguments(50)]
    [Arguments(100)]
    public async Task Api_BulkDelete_ExecuteDeleteAsync(int count)
    {
        // First create products to delete
        var products = Enumerable.Range(1, count).Select(i => new Product
        {
            Name = $"DeleteTest-{Guid.NewGuid()}",
            Description = "Delete test",
            Price = 10m,
            Stock = 10,
            CategoryId = 1
        }).ToList();

        var createJson = JsonSerializer.Serialize(products);
        var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");
        var createResponse = await _client!.PostAsync("/products/bulk", createContent);
        createResponse.EnsureSuccessStatusCode();

        var createdResult = await createResponse.Content.ReadFromJsonAsync<BulkCreateResult>();
        var productIds = createdResult?.ProductIds ?? new List<int>();

        // Delete using ExecuteDeleteAsync (zero memory, direct SQL)
        var deleteJson = JsonSerializer.Serialize(productIds);
        var deleteContent = new StringContent(deleteJson, Encoding.UTF8, "application/json");
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/products/bulk")
        {
            Content = deleteContent
        };
        var deleteResponse = await _client!.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();

        // No cleanup needed - products already deleted
    }

    [Benchmark]
    public async Task Api_ExecuteUpdate_BulkStockAdjustment()
    {
        // ExecuteUpdateAsync - Direct SQL UPDATE, zero memory
        var response = await _client!.PatchAsync("/products/bulk-update-stock?categoryId=1&stockAdjustment=10", null);
        response.EnsureSuccessStatusCode();
    }

    // =============================================================================
    // MEMORY PRESSURE TESTS - Compare traditional vs streaming approaches
    // =============================================================================
    [Benchmark]
    public async Task<int> Api_Traditional_LoadAll_10KProducts()
    {
        // Traditional approach: Load all into memory
        var response = await _client!.GetAsync("/products?page=1&pageSize=10000");
        response.EnsureSuccessStatusCode();
        var products = await response.Content.ReadFromJsonAsync<List<ProductListDto>>();
        return products?.Count ?? 0;
    }

    [Benchmark]
    public async Task<int> Api_Streaming_Process_AllProducts()
    {
        // Streaming approach: Constant memory
        var response = await _client!.GetAsync("/products/stream");
        response.EnsureSuccessStatusCode();

        int count = 0;
        await foreach (var product in response.Content.ReadFromJsonAsAsyncEnumerable<ProductListDto>())
        {
            if (product != null)
            {
                count++;
            }
        }
        return count;
    }

    // =============================================================================
    // ADDITIONAL ENTITY BENCHMARKS
    // =============================================================================
    [Benchmark]
    public async Task<List<ApexShop.API.DTOs.CategoryDto>?> Api_GetAllCategories()
    {
        var response = await _client!.GetAsync("/categories");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ApexShop.API.DTOs.CategoryDto>>();
    }

    [Benchmark]
    public async Task<ApexShop.API.DTOs.CategoryDto?> Api_GetSingleCategory()
    {
        var categoryId = Random.Shared.Next(1, 16); // 15 categories
        var response = await _client!.GetAsync($"/categories/{categoryId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApexShop.API.DTOs.CategoryDto>();
    }

    [Benchmark]
    public async Task<int> Api_StreamOrdersByUser()
    {
        var userId = Random.Shared.Next(1, 3001);
        var response = await _client!.GetAsync($"/orders/stream?userId={userId}");
        response.EnsureSuccessStatusCode();

        int count = 0;
        await foreach (var order in response.Content.ReadFromJsonAsAsyncEnumerable<OrderListDto>())
        {
            if (order != null)
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark]
    public async Task<OrderDto?> Api_GetSingleOrder()
    {
        var orderId = Random.Shared.Next(1, 5001); // Approximate order count
        var response = await _client!.GetAsync($"/orders/{orderId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderDto>();
    }

    // =============================================================================
    // HELPER CLASSES FOR DESERIALIZATION
    // =============================================================================
    private class BulkCreateResult
    {
        public int Count { get; set; }
        public string? Message { get; set; }
        public List<int>? ProductIds { get; set; }
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
