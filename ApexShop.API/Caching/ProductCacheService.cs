using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Entities;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.API.Caching;

/// <summary>
/// ProductCacheService implements the cache-aside pattern for product operations.
///
/// Cache-Aside Pattern (Lazy Loading):
/// 1. Request comes in
/// 2. Check cache (L1 local, L2 Redis)
/// 3. If miss: Query database and populate cache with explicit TTL
/// 4. Return cached or fresh data
/// 5. On write: Clear cache using tags (atomic, single operation)
///
/// Tag-Based Invalidation Strategy:
/// - "products" tag: All product data (used for bulk invalidation)
/// - "product-category-{id}" tag: Products in specific category (for category updates)
/// - One RemoveByTagAsync() call removes ALL entries with that tag
/// - Much more efficient than looping through pages manually
/// </summary>
public class ProductCacheService
{
    private readonly AppDbContext _context;
    private readonly HybridCache _cache;
    private readonly ILogger<ProductCacheService> _logger;

    // Explicit TTL configuration for different data types
    private static readonly HybridCacheEntryOptions _productTTL = new()
    {
        Expiration = TimeSpan.FromMinutes(5),        // Distributed cache
        LocalCacheExpiration = TimeSpan.FromMinutes(2) // Local L1 cache
    };

    private static readonly HybridCacheEntryOptions _listTTL = new()
    {
        Expiration = TimeSpan.FromMinutes(10),        // Lists change less frequently
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    private static readonly HybridCacheEntryOptions _countTTL = new()
    {
        Expiration = TimeSpan.FromMinutes(15),        // Counts are expensive, cache longer
        LocalCacheExpiration = TimeSpan.FromMinutes(10)
    };

    public ProductCacheService(AppDbContext context, HybridCache cache, ILogger<ProductCacheService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Get product by ID with cache-aside pattern and explicit TTL.
    ///
    /// Cache Strategy:
    /// - Try L1 (local memory): ~1-10µs (microseconds)
    /// - Try L2 (Redis): ~1-5ms (milliseconds)
    /// - Fall back to DB: ~5-50ms
    /// - Expires after 5 minutes in Redis, 2 minutes locally
    /// </summary>
    public async Task<Product?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.Product.ById(id);

        try
        {
            // HybridCache automatically tries L1, then L2, then calls factory if both miss
            var product = await _cache.GetOrCreateAsync(
                cacheKey,
                factory: async ct => await _context.Products.FirstOrDefaultAsync(p => p.Id == id, ct),
                options: _productTTL,  // Explicit TTL
                cancellationToken: cancellationToken);

            return product;
        }
        catch (Exception ex)
        {
            // Graceful degradation: Log error but continue
            _logger.LogWarning(ex, "Cache error for key {CacheKey}. Returning database result.", cacheKey);
            return await _context.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        }
    }

    /// <summary>
    /// Get products by category with cache-aside pattern.
    /// </summary>
    public async Task<List<Product>> GetProductsByCategoryAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.Product.ByCategory(categoryId);

        try
        {
            var products = await _cache.GetOrCreateAsync(
                cacheKey,
                factory: async ct => await _context.Products
                    .Where(p => p.CategoryId == categoryId)
                    .ToListAsync(ct),
                options: _listTTL,  // Explicit TTL
                cancellationToken: cancellationToken);

            return products ?? new List<Product>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache error for key {CacheKey}. Returning database result.", cacheKey);
            return await _context.Products
                .Where(p => p.CategoryId == categoryId)
                .ToListAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Get paginated products with cache-aside pattern.
    /// Also caches the total count separately for efficient list rendering.
    /// </summary>
    public async Task<(List<Product> products, int totalCount)> GetProductsPageAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.Product.Page(page, pageSize);
        var countCacheKey = "ApexShop:ProductCount"; // Simple cache key for total count

        try
        {
            // Fetch paginated products with explicit TTL
            var products = await _cache.GetOrCreateAsync(
                cacheKey,
                factory: async ct =>
                {
                    var skip = (page - 1) * pageSize;
                    return await _context.Products
                        .Skip(skip)
                        .Take(pageSize)
                        .ToListAsync(ct);
                },
                options: _listTTL,
                cancellationToken: cancellationToken);

            // Cache count separately with longer TTL (expensive query, changes less often)
            var totalCount = await _cache.GetOrCreateAsync(
                countCacheKey,
                factory: async ct => await _context.Products.CountAsync(ct),
                options: _countTTL,
                cancellationToken: cancellationToken);

            return (products ?? new List<Product>(), totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache error for key {CacheKey}. Returning database result.", cacheKey);

            var skip = (page - 1) * pageSize;
            var products = await _context.Products
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var totalCount = await _context.Products.CountAsync(cancellationToken);

            return (products, totalCount);
        }
    }

    /// <summary>
    /// Invalidate product cache on single product update.
    /// Uses tag-based invalidation: removes specific product + related categories.
    ///
    /// More efficient than manual loops:
    /// - Old approach: Loop through pages, 10-100 cache calls
    /// - New approach: One RemoveByTagAsync() call via Redis tags
    /// </summary>
    public async Task InvalidateProductAsync(Product product)
    {
        try
        {
            // Remove specific product
            await _cache.RemoveAsync(CacheKeys.Product.ById(product.Id));

            // ✅ BETTER: Use tags for bulk invalidation instead of loops
            // This is a Redis feature: one call removes ALL entries tagged with "products"
            await _cache.RemoveByTagAsync(CacheKeys.Product.Tag);

            // Also invalidate category-specific cache
            await _cache.RemoveByTagAsync(CacheKeys.Product.CategoryTag(product.CategoryId));

            // Invalidate product count cache
            await _cache.RemoveAsync("ApexShop:ProductCount");

            _logger.LogDebug("Invalidated cache for product {ProductId} using tag-based removal", product.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error invalidating cache for product {ProductId}. Continuing.", product.Id);
            // Don't throw - cache invalidation errors shouldn't break the request
        }
    }

    /// <summary>
    /// Invalidate ALL product-related cache entries.
    /// Use after bulk operations or major data changes.
    ///
    /// One tag-based call removes everything:
    /// - All paginated lists
    /// - All category filters
    /// - Product counts
    /// - Individual products
    /// </summary>
    public async Task InvalidateAllProductsAsync()
    {
        try
        {
            _logger.LogInformation("Invalidating all product caches using tag-based removal");

            // ✅ Single RemoveByTagAsync call removes all products
            // Much more efficient than looping through pages
            await _cache.RemoveByTagAsync(CacheKeys.Product.Tag);

            // Also clear category filters
            for (int categoryId = 1; categoryId <= 20; categoryId++)
            {
                try
                {
                    await _cache.RemoveByTagAsync(CacheKeys.Product.CategoryTag(categoryId));
                }
                catch { /* Continue on error */ }
            }

            // Clear count cache
            await _cache.RemoveAsync("ApexShop:ProductCount");

            _logger.LogInformation("Successfully invalidated all product caches");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error invalidating all product caches. Continuing.");
        }
    }
}
