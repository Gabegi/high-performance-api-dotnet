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

    [Benchmark]
    [WarmupCount(10)] // Extra warmup for I/O-heavy streaming operations
    public async Task<int> Api_StreamOrders_AllItems()
    {
        var response = await _client!.GetAsync("/orders/stream", HttpCompletionOption.ResponseHeadersRead);
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
    // ADDITIONAL ENTITY BENCHMARKS
    // =============================================================================
    [Benchmark]
    public async Task<int> Api_GetAllCategories()
    {
        var response = await _client!.GetAsync("/categories");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PaginatedResult<ApexShop.API.DTOs.CategoryDto>>();
        return result?.Data?.Count ?? 0;
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
        var response = await _client!.GetAsync($"/orders/stream?userId={userId}", HttpCompletionOption.ResponseHeadersRead);
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
    // ORDERS - MISSING BENCHMARKS
    // =============================================================================
    [Benchmark]
    public async Task Api_Orders_OffsetPagination_Page1()
    {
        var response = await _client!.GetAsync("/orders?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();
    }

    [Benchmark]
    public async Task Api_Orders_CursorPagination_First()
    {
        var response = await _client!.GetAsync("/orders/cursor?pageSize=50");
        response.EnsureSuccessStatusCode();
    }

    [Benchmark]
    public async Task Api_Orders_CursorPagination_Deep()
    {
        var response = await _client!.GetAsync("/orders/cursor?afterId=2500&pageSize=50");
        response.EnsureSuccessStatusCode();
    }

    [Benchmark]
    public async Task Api_Orders_BulkDeleteOld()
    {
        // ExecuteDeleteAsync - Delete old delivered orders
        var response = await _client!.DeleteAsync("/orders/bulk-delete-old?olderThanDays=730"); // 2 years old
        response.EnsureSuccessStatusCode();
    }

    // =============================================================================
    // CATEGORIES - MISSING BENCHMARKS
    // =============================================================================
    [Benchmark]
    public async Task<int> Api_Categories_StreamAll()
    {
        var response = await _client!.GetAsync("/categories/stream", HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        int count = 0;
        await foreach (var category in response.Content.ReadFromJsonAsAsyncEnumerable<CategoryListDto>())
        {
            if (category != null)
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(20)]
    public async Task Api_Categories_BulkCreate(int count)
    {
        var categories = Enumerable.Range(1, count).Select(i => new Category
        {
            Name = $"BenchCategory-{Guid.NewGuid()}",
            Description = "Benchmark test category"
        }).ToList();

        var json = JsonSerializer.Serialize(categories);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client!.PostAsync("/categories/bulk", content);
        response.EnsureSuccessStatusCode();

        // Cleanup: Delete created categories
        var result = await response.Content.ReadFromJsonAsync<BulkCreateResultGeneric>();
        if (result?.CategoryIds != null && result.CategoryIds.Count > 0)
        {
            var deleteJson = JsonSerializer.Serialize(result.CategoryIds);
            var deleteContent = new StringContent(deleteJson, Encoding.UTF8, "application/json");
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/categories/bulk")
            {
                Content = deleteContent
            };
            await _client!.SendAsync(deleteRequest);
        }
    }

    [Benchmark]
    [Arguments(10)]
    public async Task Api_Categories_BulkUpdate(int count)
    {
        // First create test categories
        var createCategories = Enumerable.Range(1, count).Select(i => new Category
        {
            Name = $"UpdateTest-{Guid.NewGuid()}",
            Description = "Update test"
        }).ToList();

        var createJson = JsonSerializer.Serialize(createCategories);
        var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");
        var createResponse = await _client!.PostAsync("/categories/bulk", createContent);
        createResponse.EnsureSuccessStatusCode();

        var createdResult = await createResponse.Content.ReadFromJsonAsync<BulkCreateResultGeneric>();
        var categoryIds = createdResult?.CategoryIds ?? new List<int>();

        // Now update them
        var updateCategories = categoryIds.Select(id => new Category
        {
            Id = (short)id,
            Name = $"Updated-{id}",
            Description = "Updated via bulk"
        }).ToList();

        var updateJson = JsonSerializer.Serialize(updateCategories);
        var updateContent = new StringContent(updateJson, Encoding.UTF8, MediaTypeNames.Application.Json);
        var updateResponse = await _client!.PutAsync("/categories/bulk", updateContent);
        updateResponse.EnsureSuccessStatusCode();

        // Cleanup: Delete created categories
        if (categoryIds.Count > 0)
        {
            var deleteJson = JsonSerializer.Serialize(categoryIds);
            var deleteContent = new StringContent(deleteJson, Encoding.UTF8, "application/json");
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/categories/bulk")
            {
                Content = deleteContent
            };
            await _client!.SendAsync(deleteRequest);
        }
    }

    [Benchmark]
    [Arguments(10)]
    public async Task Api_Categories_BulkDelete(int count)
    {
        // First create categories to delete
        var categories = Enumerable.Range(1, count).Select(i => new Category
        {
            Name = $"DeleteTest-{Guid.NewGuid()}",
            Description = "Delete test"
        }).ToList();

        var createJson = JsonSerializer.Serialize(categories);
        var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");
        var createResponse = await _client!.PostAsync("/categories/bulk", createContent);
        createResponse.EnsureSuccessStatusCode();

        var createdResult = await createResponse.Content.ReadFromJsonAsync<BulkCreateResultGeneric>();
        var categoryIds = createdResult?.CategoryIds ?? new List<int>();

        // Delete using ExecuteDeleteAsync
        var deleteJson = JsonSerializer.Serialize(categoryIds);
        var deleteContent = new StringContent(deleteJson, Encoding.UTF8, "application/json");
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/categories/bulk")
        {
            Content = deleteContent
        };
        var deleteResponse = await _client!.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();
    }

    // =============================================================================
    // REVIEWS - ALL BENCHMARKS
    // =============================================================================
    [Benchmark]
    public async Task<ReviewDto?> Api_Reviews_GetSingle()
    {
        var reviewId = Random.Shared.Next(1, 10001); // Approximate review count
        var response = await _client!.GetAsync($"/reviews/{reviewId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ReviewDto>();
    }

    [Benchmark]
    public async Task Api_Reviews_OffsetPagination_Page1()
    {
        var response = await _client!.GetAsync("/reviews?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();
    }

    [Benchmark]
    public async Task Api_Reviews_CursorPagination_First()
    {
        var response = await _client!.GetAsync("/reviews/cursor?pageSize=50");
        response.EnsureSuccessStatusCode();
    }

    [Benchmark]
    public async Task Api_Reviews_CursorPagination_Deep()
    {
        var response = await _client!.GetAsync("/reviews/cursor?afterId=5000&pageSize=50");
        response.EnsureSuccessStatusCode();
    }

    [Benchmark]
    [WarmupCount(10)] // Extra warmup for large dataset streaming operations
    public async Task<int> Api_Reviews_StreamFiltered()
    {
        var response = await _client!.GetAsync("/reviews/stream?minRating=4", HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        int count = 0;
        await foreach (var review in response.Content.ReadFromJsonAsAsyncEnumerable<ReviewListDto>())
        {
            if (review != null)
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark]
    [Arguments(20)]
    [Arguments(50)]
    public async Task Api_Reviews_BulkCreate(int count)
    {
        var reviews = Enumerable.Range(1, count).Select(i => new Review
        {
            ProductId = Random.Shared.Next(1, 1001),
            UserId = Random.Shared.Next(1, 501),
            Rating = 5,
            Comment = "Benchmark test review",
            IsVerifiedPurchase = true
        }).ToList();

        var json = JsonSerializer.Serialize(reviews);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client!.PostAsync("/reviews/bulk", content);
        response.EnsureSuccessStatusCode();

        // Cleanup: Delete created reviews
        var result = await response.Content.ReadFromJsonAsync<BulkCreateResultGeneric>();
        if (result?.ReviewIds != null && result.ReviewIds.Count > 0)
        {
            var deleteJson = JsonSerializer.Serialize(result.ReviewIds);
            var deleteContent = new StringContent(deleteJson, Encoding.UTF8, "application/json");
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/reviews/bulk")
            {
                Content = deleteContent
            };
            await _client!.SendAsync(deleteRequest);
        }
    }

    [Benchmark]
    [Arguments(20)]
    public async Task Api_Reviews_BulkUpdate(int count)
    {
        // First create test reviews
        var createReviews = Enumerable.Range(1, count).Select(i => new Review
        {
            ProductId = 1,
            UserId = 1,
            Rating = 3,
            Comment = "Update test",
            IsVerifiedPurchase = true
        }).ToList();

        var createJson = JsonSerializer.Serialize(createReviews);
        var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");
        var createResponse = await _client!.PostAsync("/reviews/bulk", createContent);
        createResponse.EnsureSuccessStatusCode();

        var createdResult = await createResponse.Content.ReadFromJsonAsync<BulkCreateResultGeneric>();
        var reviewIds = createdResult?.ReviewIds ?? new List<int>();

        // Now update them
        var updateReviews = reviewIds.Select(id => new Review
        {
            Id = id,
            ProductId = 1,
            UserId = 1,
            Rating = 5,
            Comment = "Updated via bulk",
            IsVerifiedPurchase = true
        }).ToList();

        var updateJson = JsonSerializer.Serialize(updateReviews);
        var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");
        var updateResponse = await _client!.PutAsync("/reviews/bulk", updateContent);
        updateResponse.EnsureSuccessStatusCode();

        // Cleanup: Delete created reviews
        if (reviewIds.Count > 0)
        {
            var deleteJson = JsonSerializer.Serialize(reviewIds);
            var deleteContent = new StringContent(deleteJson, Encoding.UTF8, "application/json");
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/reviews/bulk")
            {
                Content = deleteContent
            };
            await _client!.SendAsync(deleteRequest);
        }
    }

    [Benchmark]
    [Arguments(20)]
    public async Task Api_Reviews_BulkDelete(int count)
    {
        // First create reviews to delete
        var reviews = Enumerable.Range(1, count).Select(i => new Review
        {
            ProductId = 1,
            UserId = 1,
            Rating = 4,
            Comment = "Delete test",
            IsVerifiedPurchase = true
        }).ToList();

        var createJson = JsonSerializer.Serialize(reviews);
        var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");
        var createResponse = await _client!.PostAsync("/reviews/bulk", createContent);
        createResponse.EnsureSuccessStatusCode();

        var createdResult = await createResponse.Content.ReadFromJsonAsync<BulkCreateResultGeneric>();
        var reviewIds = createdResult?.ReviewIds ?? new List<int>();

        // Delete using ExecuteDeleteAsync
        var deleteJson = JsonSerializer.Serialize(reviewIds);
        var deleteContent = new StringContent(deleteJson, Encoding.UTF8, "application/json");
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/reviews/bulk")
        {
            Content = deleteContent
        };
        var deleteResponse = await _client!.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();
    }

    // =============================================================================
    // USERS - ALL BENCHMARKS
    // =============================================================================
    [Benchmark]
    public async Task<UserDto?> Api_Users_GetSingle()
    {
        var userId = Random.Shared.Next(1, 3001);
        var response = await _client!.GetAsync($"/users/{userId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<UserDto>();
    }

    [Benchmark]
    public async Task Api_Users_OffsetPagination_Page1()
    {
        var response = await _client!.GetAsync("/users?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();
    }

    [Benchmark]
    public async Task Api_Users_CursorPagination_First()
    {
        var response = await _client!.GetAsync("/users/cursor?pageSize=50");
        response.EnsureSuccessStatusCode();
    }

    [Benchmark]
    public async Task Api_Users_CursorPagination_Deep()
    {
        var response = await _client!.GetAsync("/users/cursor?afterId=1500&pageSize=50");
        response.EnsureSuccessStatusCode();
    }

    [Benchmark]
    [WarmupCount(10)] // Extra warmup for filtered streaming with I/O variance
    public async Task<int> Api_Users_StreamFiltered()
    {
        var response = await _client!.GetAsync("/users/stream?isActive=true", HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        int count = 0;
        await foreach (var user in response.Content.ReadFromJsonAsAsyncEnumerable<UserListDto>())
        {
            if (user != null)
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(20)]
    public async Task Api_Users_BulkCreate(int count)
    {
        var users = Enumerable.Range(1, count).Select(i => new User
        {
            Email = $"bench-user-{Guid.NewGuid()}@example.com",
            PasswordHash = "benchmarkhash",
            FirstName = "Bench",
            LastName = "User",
            PhoneNumber = "0000000000",
            IsActive = true
        }).ToList();

        var json = JsonSerializer.Serialize(users);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _client!.PostAsync("/users/bulk", content);
        response.EnsureSuccessStatusCode();

        // Cleanup: Delete created users
        var result = await response.Content.ReadFromJsonAsync<BulkCreateResultGeneric>();
        if (result?.UserIds != null && result.UserIds.Count > 0)
        {
            var deleteJson = JsonSerializer.Serialize(result.UserIds);
            var deleteContent = new StringContent(deleteJson, Encoding.UTF8, "application/json");
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/users/bulk")
            {
                Content = deleteContent
            };
            await _client!.SendAsync(deleteRequest);
        }
    }

    [Benchmark]
    [Arguments(10)]
    public async Task Api_Users_BulkUpdate(int count)
    {
        // First create test users
        var createUsers = Enumerable.Range(1, count).Select(i => new User
        {
            Email = $"update-test-{Guid.NewGuid()}@example.com",
            PasswordHash = "testhash",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "1111111111",
            IsActive = true
        }).ToList();

        var createJson = JsonSerializer.Serialize(createUsers);
        var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");
        var createResponse = await _client!.PostAsync("/users/bulk", createContent);
        createResponse.EnsureSuccessStatusCode();

        var createdResult = await createResponse.Content.ReadFromJsonAsync<BulkCreateResultGeneric>();
        var userIds = createdResult?.UserIds ?? new List<int>();

        // Now update them
        var updateUsers = userIds.Select(id => new User
        {
            Id = id,
            Email = $"updated-{id}@example.com",
            PasswordHash = "updatedhash",
            FirstName = "Updated",
            LastName = "User",
            PhoneNumber = "2222222222",
            IsActive = true
        }).ToList();

        var updateJson = JsonSerializer.Serialize(updateUsers);
        var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");
        var updateResponse = await _client!.PutAsync("/users/bulk", updateContent);
        updateResponse.EnsureSuccessStatusCode();

        // Cleanup: Delete created users
        if (userIds.Count > 0)
        {
            var deleteJson = JsonSerializer.Serialize(userIds);
            var deleteContent = new StringContent(deleteJson, Encoding.UTF8, "application/json");
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/users/bulk")
            {
                Content = deleteContent
            };
            await _client!.SendAsync(deleteRequest);
        }
    }

    [Benchmark]
    [Arguments(10)]
    public async Task Api_Users_BulkDelete(int count)
    {
        // First create users to delete
        var users = Enumerable.Range(1, count).Select(i => new User
        {
            Email = $"delete-test-{Guid.NewGuid()}@example.com",
            PasswordHash = "deletehash",
            FirstName = "Delete",
            LastName = "Test",
            PhoneNumber = "3333333333",
            IsActive = true
        }).ToList();

        var createJson = JsonSerializer.Serialize(users);
        var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");
        var createResponse = await _client!.PostAsync("/users/bulk", createContent);
        createResponse.EnsureSuccessStatusCode();

        var createdResult = await createResponse.Content.ReadFromJsonAsync<BulkCreateResultGeneric>();
        var userIds = createdResult?.UserIds ?? new List<int>();

        // Delete using ExecuteDeleteAsync
        var deleteJson = JsonSerializer.Serialize(userIds);
        var deleteContent = new StringContent(deleteJson, Encoding.UTF8, "application/json");
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/users/bulk")
        {
            Content = deleteContent
        };
        var deleteResponse = await _client!.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();
    }

    [Benchmark]
    public async Task Api_Users_ExecuteUpdate_DeactivateInactive()
    {
        // ExecuteUpdateAsync - Deactivate users inactive for 730+ days
        var response = await _client!.PatchAsync("/users/bulk-deactivate-inactive?inactiveDays=730", null);
        response.EnsureSuccessStatusCode();
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
