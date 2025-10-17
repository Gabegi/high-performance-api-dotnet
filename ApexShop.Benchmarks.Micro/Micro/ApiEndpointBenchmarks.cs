using ApexShop.API.DTOs;
using ApexShop.Infrastructure.Entities;
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
    private HttpClient? _client;
    private WebApplicationFactory<ApexShop.API.Program>? _factory;

    // Reusable helper DTOs for parsing server wrappers
    public class PagedResponse<T>
    {
        public List<T>? Data { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }

    public class CursorResponse<T>
    {
        public List<T>? Data { get; set; }
        public int PageSize { get; set; }
        public bool HasMore { get; set; }
        public int? NextCursor { get; set; }
    }

    private class BulkCreatedGeneric
    {
        // not used directly for typed deserialization, we parse JSON dynamically
    }

    // =============================================================================
    // GLOBAL SETUP
    // =============================================================================
    [GlobalSetup]
    public void Setup()
    {
        _factory = new WebApplicationFactory<ApexShop.API.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production"); // Use Production to minimize noise
            });

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            // Example: use relative paths, not BaseAddress overrides
            AllowAutoRedirect = false
        });

        // Set timeout directly on the HttpClient (not via options)
        _client.Timeout = TimeSpan.FromMinutes(5);
    }


    // =============================================================================
    // HELPER: Extract IDs from bulk create responses
    // - Bulk responses differ in property name (ProductIds, CategoryIds, UserIds, ReviewIds)
    // - This helper finds the first property that ends with "Ids" and returns int list
    // =============================================================================
    private static async Task<List<int>> ExtractIdsFromBulkResponse(HttpContent content)
    {
        if (content == null) return new List<int>();

        await using var stream = await content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name.EndsWith("Ids", StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<int>();
                    foreach (var el in prop.Value.EnumerateArray())
                    {
                        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var id))
                            list.Add(id);
                    }
                    return list;
                }
            }
        }

        // fallback: try to find any array of ints under any property
        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<int>();
                    foreach (var el in prop.Value.EnumerateArray())
                    {
                        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var id))
                            list.Add(id);
                    }
                    if (list.Count > 0) return list;
                }
            }
        }

        return new List<int>();
    }

    // =============================================================================
    // PRODUCTS - Cold start / Get single / List / Stream / Cursor / Bulk / BulkUpdate / BulkDelete / ExecuteUpdate
    // =============================================================================

    [Benchmark]
    public async Task<Product?> Api_Product_ColdStart()
    {
        using var factory = new WebApplicationFactory<ApexShop.API.Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/products/1");
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ProductDto>();
        if (dto is null) return null;

        // Return an entity approximation for consistency with previous signature
        return new Product
        {
            Id = dto.Id,
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            Stock = (short)dto.Stock,
            CategoryId = (short)dto.CategoryId
        };
    }

    [Benchmark(Baseline = true)]
    public async Task<ProductListDto?> Api_Product_GetSingleRandom()
    {
        var productId = Random.Shared.Next(1, 15001);
        var response = await _client!.GetAsync($"/products/{productId}");
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<ProductDto>();
        if (dto == null) return null;

        return new ProductListDto(dto.Id, dto.Name, dto.Price, dto.Stock, dto.CategoryId);
    }

    [Benchmark]
    public async Task<PagedResponse<ProductListDto>?> Api_Product_GetPaged_FirstPage()
    {
        var response = await _client!.GetAsync("/products?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResponse<ProductListDto>>();
    }

    [Benchmark]
    public async Task<PagedResponse<ProductListDto>?> Api_Product_GetPaged_Page100()
    {
        var response = await _client!.GetAsync("/products?page=100&pageSize=50");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResponse<ProductListDto>>();
    }

    [Benchmark]
    public async Task<CursorResponse<ProductListDto>?> Api_Product_GetCursor_First()
    {
        var response = await _client!.GetAsync("/products/cursor?pageSize=50");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CursorResponse<ProductListDto>>();
    }

    [Benchmark]
    public async Task<CursorResponse<ProductListDto>?> Api_Product_GetCursor_Deep()
    {
        var response = await _client!.GetAsync("/products/cursor?afterId=5000&pageSize=50");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CursorResponse<ProductListDto>>();
    }

    [Benchmark]
    public async Task<int> Api_Product_Stream_AllItems()
    {
        var response = await _client!.GetAsync("/products/stream");
        response.EnsureSuccessStatusCode();

        int count = 0;
        await foreach (var product in response.Content.ReadFromJsonAsAsyncEnumerable<ProductListDto>())
        {
            if (product != null) count++;
        }
        return count;
    }

    [Benchmark]
    public async Task<int> Api_Product_Stream_Filtered_Category1()
    {
        var response = await _client!.GetAsync("/products/stream?categoryId=1");
        response.EnsureSuccessStatusCode();

        int count = 0;
        await foreach (var product in response.Content.ReadFromJsonAsAsyncEnumerable<ProductListDto>())
        {
            if (product != null) count++;
        }
        return count;
    }

    [Benchmark]
    [Arguments(50)]
    [Arguments(100)]
    [Arguments(500)]
    public async Task Api_Product_BulkCreate_AndCleanup(int count)
    {
        // Build products
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

        var ids = await ExtractIdsFromBulkResponse(response.Content);

        if (ids.Count > 0)
        {
            // Delete created products using bulk delete endpoint
            var deleteJson = JsonSerializer.Serialize(ids);
            var deleteContent = new StringContent(deleteJson, Encoding.UTF8, "application/json");
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/products/bulk")
            {
                Content = deleteContent
            };
            var deleteResponse = await _client!.SendAsync(deleteRequest);
            deleteResponse.EnsureSuccessStatusCode();
        }
    }

    [Benchmark]
    [Arguments(50)]
    [Arguments(100)]
    public async Task Api_Product_BulkUpdate_Streaming(int count)
    {
        // Create products to update
        var createProds = Enumerable.Range(1, count).Select(i => new Product
        {
            Name = $"UpdateTest-{Guid.NewGuid()}",
            Description = "Update test",
            Price = 50m,
            Stock = 50,
            CategoryId = 1
        }).ToList();

        var createResponse = await _client!.PostAsync("/products/bulk", new StringContent(JsonSerializer.Serialize(createProds), Encoding.UTF8, "application/json"));
        createResponse.EnsureSuccessStatusCode();
        var createdIds = await ExtractIdsFromBulkResponse(createResponse.Content);

        // prepare updates
        var updateProducts = createdIds.Select(id => new Product
        {
            Id = id,
            Name = $"Updated-{id}",
            Description = "Updated via bulk",
            Price = 75m,
            Stock = 75,
            CategoryId = 1
        }).ToList();

        var updateResponse = await _client!.PutAsync("/products/bulk", new StringContent(JsonSerializer.Serialize(updateProducts), Encoding.UTF8, "application/json"));
        updateResponse.EnsureSuccessStatusCode();

        // cleanup
        if (createdIds.Count > 0)
        {
            var deleteContent = new StringContent(JsonSerializer.Serialize(createdIds), Encoding.UTF8, "application/json");
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/products/bulk") { Content = deleteContent };
            var deleteResponse = await _client!.SendAsync(deleteRequest);
            deleteResponse.EnsureSuccessStatusCode();
        }
    }

    [Benchmark]
    [Arguments(50)]
    [Arguments(100)]
    public async Task Api_Product_BulkDelete_ExecuteDeleteAsync(int count)
    {
        // create items to delete
        var products = Enumerable.Range(1, count).Select(i => new Product
        {
            Name = $"DeleteTest-{Guid.NewGuid()}",
            Description = "Delete test",
            Price = 10m,
            Stock = 10,
            CategoryId = 1
        }).ToList();

        var createResponse = await _client!.PostAsync("/products/bulk", new StringContent(JsonSerializer.Serialize(products), Encoding.UTF8, "application/json"));
        createResponse.EnsureSuccessStatusCode();
        var createdIds = await ExtractIdsFromBulkResponse(createResponse.Content);

        var deleteContent = new StringContent(JsonSerializer.Serialize(createdIds), Encoding.UTF8, "application/json");
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/products/bulk") { Content = deleteContent };
        var deleteResponse = await _client!.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();
    }

    [Benchmark]
    public async Task Api_Product_ExecuteUpdate_BulkStockAdjustment()
    {
        var response = await _client!.PatchAsync("/products/bulk-update-stock?categoryId=1&stockAdjustment=10", null);
        response.EnsureSuccessStatusCode();
    }

    [Benchmark]
    public async Task<int> Api_Product_Traditional_LoadAll_10KProducts()
    {
        var response = await _client!.GetAsync("/products?page=1&pageSize=10000");
        response.EnsureSuccessStatusCode();
        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<ProductListDto>>();
        return paged?.Data?.Count ?? 0;
    }

    [Benchmark]
    public async Task<int> Api_Product_Streaming_Process_AllProducts()
    {
        var response = await _client!.GetAsync("/products/stream");
        response.EnsureSuccessStatusCode();

        int count = 0;
        await foreach (var product in response.Content.ReadFromJsonAsAsyncEnumerable<ProductListDto>())
        {
            if (product != null) count++;
        }
        return count;
    }

    // =============================================================================
    // ORDERS
    // =============================================================================

    [Benchmark]
    public async Task<PagedResponse<OrderListDto>?> Api_Order_GetPaged_FirstPage()
    {
        var response = await _client!.GetAsync("/orders?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResponse<OrderListDto>>();
    }

    [Benchmark]
    public async Task<CursorResponse<OrderListDto>?> Api_Order_GetCursor_First()
    {
        var response = await _client!.GetAsync("/orders/cursor?pageSize=50");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CursorResponse<OrderListDto>>();
    }

    [Benchmark]
    public async Task<int> Api_Order_Stream_ByUser()
    {
        var userId = Random.Shared.Next(1, 3001);
        var response = await _client!.GetAsync($"/orders/stream?userId={userId}");
        response.EnsureSuccessStatusCode();

        int count = 0;
        await foreach (var order in response.Content.ReadFromJsonAsAsyncEnumerable<OrderListDto>())
        {
            if (order != null) count++;
        }
        return count;
    }

    [Benchmark]
    public async Task<OrderDto?> Api_Order_GetSingleRandom()
    {
        var orderId = Random.Shared.Next(1, 5001);
        var response = await _client!.GetAsync($"/orders/{orderId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<OrderDto>();
    }

    [Benchmark]
    public async Task Api_Order_BulkDeleteOld(int olderThanDays = 365)
    {
        var response = await _client!.DeleteAsync($"/orders/bulk-delete-old?olderThanDays={olderThanDays}");
        response.EnsureSuccessStatusCode();
    }

    // =============================================================================
    // CATEGORIES
    // =============================================================================

    [Benchmark]
    public async Task<PagedResponse<CategoryListDto>?> Api_Category_GetPaged_FirstPage()
    {
        var response = await _client!.GetAsync("/categories?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResponse<CategoryListDto>>();
    }

    [Benchmark]
    public async Task<int> Api_Category_Stream_All()
    {
        var response = await _client!.GetAsync("/categories/stream");
        response.EnsureSuccessStatusCode();

        int count = 0;
        await foreach (var c in response.Content.ReadFromJsonAsAsyncEnumerable<CategoryListDto>())
        {
            if (c != null) count++;
        }
        return count;
    }

    [Benchmark]
    public async Task<CategoryDto?> Api_Category_GetSingleRandom()
    {
        var categoryId = Random.Shared.Next(1, 16);
        var response = await _client!.GetAsync($"/categories/{categoryId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<CategoryDto>();
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(50)]
    public async Task Api_Category_BulkCreate_AndCleanup(int count)
    {
        var categories = Enumerable.Range(1, count).Select(i => new Category
        {
            Name = $"BenchCategory-{Guid.NewGuid()}",
            Description = "Benchmark category"
        }).ToList();

        var createResponse = await _client!.PostAsync("/categories/bulk", new StringContent(JsonSerializer.Serialize(categories), Encoding.UTF8, "application/json"));
        createResponse.EnsureSuccessStatusCode();
        var ids = await ExtractIdsFromBulkResponse(createResponse.Content);

        if (ids.Count > 0)
        {
            var deleteReq = new HttpRequestMessage(HttpMethod.Delete, "/categories/bulk")
            {
                Content = new StringContent(JsonSerializer.Serialize(ids), Encoding.UTF8, "application/json")
            };
            var deleteResp = await _client!.SendAsync(deleteReq);
            deleteResp.EnsureSuccessStatusCode();
        }
    }

    // =============================================================================
    // REVIEWS
    // =============================================================================

    [Benchmark]
    public async Task<PagedResponse<ReviewListDto>?> Api_Review_GetPaged_FirstPage()
    {
        var response = await _client!.GetAsync("/reviews?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResponse<ReviewListDto>>();
    }

    [Benchmark]
    public async Task<int> Api_Review_Stream_Filtered()
    {
        var response = await _client!.GetAsync("/reviews/stream?minRating=4");
        response.EnsureSuccessStatusCode();

        int count = 0;
        await foreach (var r in response.Content.ReadFromJsonAsAsyncEnumerable<ReviewListDto>())
        {
            if (r != null) count++;
        }
        return count;
    }

    [Benchmark]
    [Arguments(20)]
    public async Task Api_Review_BulkCreate_AndCleanup(int count)
    {
        var reviews = Enumerable.Range(1, count).Select(i => new Review
        {
            ProductId = 1,
            UserId = 1,
            Rating = 5,
            Comment = "Benchmark",
            IsVerifiedPurchase = true
        }).ToList();

        var createResp = await _client!.PostAsync("/reviews/bulk", new StringContent(JsonSerializer.Serialize(reviews), Encoding.UTF8, "application/json"));
        createResp.EnsureSuccessStatusCode();
        var ids = await ExtractIdsFromBulkResponse(createResp.Content);

        if (ids.Count > 0)
        {
            var deleteReq = new HttpRequestMessage(HttpMethod.Delete, "/reviews/bulk")
            {
                Content = new StringContent(JsonSerializer.Serialize(ids), Encoding.UTF8, "application/json")
            };
            var deleteResp = await _client!.SendAsync(deleteReq);
            deleteResp.EnsureSuccessStatusCode();
        }
    }

    // =============================================================================
    // USERS
    // =============================================================================

    [Benchmark]
    public async Task<PagedResponse<UserListDto>?> Api_User_GetPaged_FirstPage()
    {
        var response = await _client!.GetAsync("/users?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResponse<UserListDto>>();
    }

    [Benchmark]
    public async Task<int> Api_User_Stream_Filtered()
    {
        var response = await _client!.GetAsync("/users/stream?isActive=true");
        response.EnsureSuccessStatusCode();

        int count = 0;
        await foreach (var u in response.Content.ReadFromJsonAsAsyncEnumerable<UserListDto>())
        {
            if (u != null) count++;
        }
        return count;
    }

    [Benchmark]
    [Arguments(10)]
    public async Task Api_User_BulkCreate_AndCleanup(int count)
    {
        var users = Enumerable.Range(1, count).Select(i => new User
        {
            Email = $"bench-user-{Guid.NewGuid()}@example.com",
            PasswordHash = "hash",
            FirstName = "Bench",
            LastName = "User",
            PhoneNumber = "0000000000",
            IsActive = true
        }).ToList();

        var createResp = await _client!.PostAsync("/users/bulk", new StringContent(JsonSerializer.Serialize(users), Encoding.UTF8, "application/json"));
        createResp.EnsureSuccessStatusCode();
        var ids = await ExtractIdsFromBulkResponse(createResp.Content);

        if (ids.Count > 0)
        {
            var deleteReq = new HttpRequestMessage(HttpMethod.Delete, "/users/bulk")
            {
                Content = new StringContent(JsonSerializer.Serialize(ids), Encoding.UTF8, "application/json")
            };
            var deleteResp = await _client!.SendAsync(deleteReq);
            deleteResp.EnsureSuccessStatusCode();
        }
    }

    [Benchmark]
    public async Task Api_User_ExecuteUpdate_DeactivateInactive()
    {
        var response = await _client!.PatchAsync("/users/bulk-deactivate-inactive?inactiveDays=365", null);
        response.EnsureSuccessStatusCode();
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
    // CONFIG (keeps your existing config behavior)
    // =============================================================================
    public class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var reportsDir = Path.Combine(
                GetSolutionRoot() ?? AppContext.BaseDirectory,
                "ApexShop.Benchmarks.Micro",
                "Reports",
                timestamp
            );
            ArtifactsPath = Path.GetFullPath(reportsDir);

            AddExporter(BenchmarkDotNet.Exporters.Csv.CsvExporter.Default);
            AddExporter(BenchmarkDotNet.Exporters.HtmlExporter.Default);
        }

        static string? GetSolutionRoot()
        {
            var directory = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
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
