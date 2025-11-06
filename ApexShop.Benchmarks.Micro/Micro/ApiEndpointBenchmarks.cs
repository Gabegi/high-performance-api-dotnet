using ApexShop.Infrastructure.Entities;
using ApexShop.API.DTOs;
using ApexShop.API.JsonContext;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Diagnostics.Windows;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace ApexShop.Benchmarks.Micro;

// =============================================================================
// VARIANCE REDUCTION TIPS:
// =============================================================================
// Streaming benchmarks (100ms+) have inherent 10-30% variance due to:
// - Disk I/O variability (OS page cache state)
// - Garbage collection pauses (500+ Gen0 collections for large datasets)
// - Network buffering and TCP window scaling
// - OS scheduler interrupts and context switches
//
// To reduce variance further:
// 1. ✅ Increased WarmupCount(10) for streaming ops (already implemented)
// 2. Close background apps (browsers, Slack, Windows Defender, etc.)
// 3. Disable Windows Search indexing during benchmarks
// 4. Run on Linux for lower OS overhead (optional)
// 5. Use SSD storage to reduce I/O variance
//
// Single-entity GETs (<3ms) have excellent 7% variance and need no tuning.
// =============================================================================

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
[WarmupCount(5)]  // Default warmup for fast operations
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
        _factory = new WebApplicationFactory<ApexShop.API.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production"); // Use Production mode to disable verbose logging
            });
        _client = _factory.CreateClient();
    }

    // =============================================================================
    // HELPER METHODS FOR NDJSON EXPORT BENCHMARKS
    // =============================================================================

    /// <summary>
    /// Process NDJSON stream with error resilience.
    /// Skips malformed lines to demonstrate NDJSON's error recovery benefit.
    /// Advantage: Single malformed line doesn't fail entire export (vs JSON array fails completely).
    /// </summary>
    private async Task<int> ProcessNdjsonStream(HttpResponseMessage response)
    {
        int count = 0;
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                // Parse NDJSON line (JsonDocument is lightweight - doesn't allocate full object)
                using var doc = JsonDocument.Parse(line);
                count++;
            }
            catch (JsonException)
            {
                // NDJSON error recovery: Skip malformed lines, continue processing
                // JSON array would fail completely on first error
                continue;
            }
        }
        return count;
    }

    /// <summary>
    /// Measure time to first parseable item in NDJSON stream.
    /// Demonstrates NDJSON's progressive parsing advantage vs JSON arrays.
    ///
    /// Timing includes: Network I/O (ReadLineAsync) + JSON parsing
    /// This reflects real-world "time to first usable item" performance.
    ///
    /// Returns: Time until first valid JSON line is read and parsed
    /// Edge case: Empty stream returns TimeSpan.Zero (no valid items)
    ///
    /// NDJSON advantage: Client can process first item while server sends remaining items
    /// JSON array disadvantage: Must wait for closing ']' bracket before any processing
    /// </summary>
    private async Task<TimeSpan> ProcessNdjsonStreamTimeToFirst(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        // ✅ Stopwatch starts HERE - timing I/O + parse (time to first usable item)
        var stopwatch = Stopwatch.StartNew();

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                stopwatch.Stop();
                return stopwatch.Elapsed; // Time to get + parse first valid item
            }
            catch (JsonException)
            {
                continue;
            }
        }

        stopwatch.Stop();
        // Empty stream: no valid items found
        return TimeSpan.Zero;
    }

    /// <summary>
    /// Measure time to parse first N items in NDJSON stream.
    /// Demonstrates progressive parsing across multiple records.
    ///
    /// Returns: List of cumulative timings for each successfully parsed item
    /// Example: [100ms, 105ms, 110ms] = first 3 items took 100ms, 105ms, 110ms
    /// </summary>
    private async Task<List<TimeSpan>> ProcessNdjsonStreamTimeToN(
        HttpResponseMessage response,
        int n)
    {
        var timings = new List<TimeSpan>();
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        // ✅ Stopwatch starts HERE - timing from first parse operation
        var stopwatch = Stopwatch.StartNew();

        string? line;
        int count = 0;

        while ((line = await reader.ReadLineAsync()) != null && count < n)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                timings.Add(stopwatch.Elapsed);
                count++;
            }
            catch (JsonException)
            {
                // Skip malformed lines but continue timing
                continue;
            }
        }

        stopwatch.Stop();
        return timings;
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
    public async Task<int> Api_GetAllProducts()
    {
        var response = await _client!.GetAsync("/products");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<Product>>();
        return result?.Data?.Count ?? 0;
    }

    // =============================================================================
    // STREAMING TESTS - Database-level streaming with JSON array serialization overhead
    // =============================================================================
    // NOTE: These endpoints use EF Core's AsAsyncEnumerable() for constant-memory database streaming.
    // However, ASP.NET Core serializes to JSON arrays `[...]`, and System.Text.Json's
    // ReadFromJsonAsAsyncEnumerable() must buffer array data during deserialization.
    // Result: ~650KB per 1,000 records buffered. For true O(1) memory with large datasets,
    // use cursor pagination (/products/cursor) instead which has constant memory AND O(1) performance.
    // To achieve true streaming, the API would need to use NDJSON (newline-delimited JSON).
    //
    // VARIANCE NOTE: Streaming operations have higher variance (10-30%) due to:
    // - Disk I/O variability (OS page cache state)
    // - Garbage collection pauses (500+ Gen0 collections for 15K items)
    // - Network buffering and TCP windowing
    // - OS scheduler interrupts
    // Increased warmup iterations help stabilize JIT compilation and cache warmup.
    // =============================================================================
    [Benchmark]
    [WarmupCount(10)] // Extra warmup for I/O-heavy operations to stabilize variance
    public async Task<int> Api_StreamProducts_AllItems()
    {
        // Stream all products - database streams, but JSON array adds ~9MB overhead for 15K records
        // HttpCompletionOption.ResponseHeadersRead: Don't buffer entire response, start streaming immediately
        var response = await _client!.GetAsync("/products/stream", HttpCompletionOption.ResponseHeadersRead);
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
    [WarmupCount(10)] // Extra warmup for I/O-heavy streaming operations
    public async Task<int> Api_StreamProducts_Limited1000()
    {
        // Stream limited items using category filter (first 1000 approx)
        var response = await _client!.GetAsync("/products/stream?categoryId=1", HttpCompletionOption.ResponseHeadersRead);
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

        var json = JsonSerializer.Serialize(products, ApexShopJsonContext.Default.ListProduct);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client!.PostAsync("/products/bulk", content);
        response.EnsureSuccessStatusCode();

        // Cleanup: Delete created products to prevent data pollution
        var result = await response.Content.ReadFromJsonAsync<BulkCreateResult>();
        if (result?.ProductIds != null && result.ProductIds.Count > 0)
        {
            var deleteJson = JsonSerializer.Serialize(result.ProductIds, ApexShopJsonContext.Default.ListInt32);
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

        var createJson = JsonSerializer.Serialize(createProducts, ApexShopJsonContext.Default.ListProduct);
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

        var updateJson = JsonSerializer.Serialize(updateProducts, ApexShopJsonContext.Default.ListProduct);
        var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");
        var updateResponse = await _client!.PutAsync("/products/bulk", updateContent);
        updateResponse.EnsureSuccessStatusCode();

        // Cleanup: Delete created products
        if (productIds.Count > 0)
        {
            var deleteJson = JsonSerializer.Serialize(productIds, ApexShopJsonContext.Default.ListInt32);
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

        var createJson = JsonSerializer.Serialize(products, ApexShopJsonContext.Default.ListProduct);
        var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");
        var createResponse = await _client!.PostAsync("/products/bulk", createContent);
        createResponse.EnsureSuccessStatusCode();

        var createdResult = await createResponse.Content.ReadFromJsonAsync<BulkCreateResult>();
        var productIds = createdResult?.ProductIds ?? new List<int>();

        // Delete using ExecuteDeleteAsync (zero memory, direct SQL)
        var deleteJson = JsonSerializer.Serialize(productIds, ApexShopJsonContext.Default.ListInt32);
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
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ProductListDto>>();
        return result?.Data?.Count ?? 0;
    }

    [Benchmark]
    [WarmupCount(10)] // Extra warmup to reduce variance from GC and I/O
    public async Task<int> Api_Streaming_Process_AllProducts()
    {
        // Streaming approach: Constant memory
        var response = await _client!.GetAsync("/products/stream", HttpCompletionOption.ResponseHeadersRead);
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
    // HELPER CLASSES FOR DESERIALIZATION
    // =============================================================================
    private class PaginatedResult<T>
    {
        public List<T>? Data { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }

    private class BulkCreateResult
    {
        public int Count { get; set; }
        public string? Message { get; set; }
        public List<int>? ProductIds { get; set; }
    }

    private class BulkCreateResultGeneric
    {
        public int Count { get; set; }
        public string? Message { get; set; }
        public List<int>? ProductIds { get; set; }
        public List<int>? CategoryIds { get; set; }
        public List<int>? ReviewIds { get; set; }
        public List<int>? UserIds { get; set; }
        public List<int>? OrderIds { get; set; }
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
