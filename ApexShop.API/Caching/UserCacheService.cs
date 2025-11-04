using ApexShop.Infrastructure.Data;
using ApexShop.Infrastructure.Entities;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.API.Caching;

/// <summary>
/// UserCacheService implements the cache-aside pattern for user operations.
/// Caches user profiles which are read-heavy and change infrequently.
/// </summary>
public class UserCacheService
{
    private readonly AppDbContext _context;
    private readonly HybridCache _cache;
    private readonly ILogger<UserCacheService> _logger;

    public UserCacheService(AppDbContext context, HybridCache cache, ILogger<UserCacheService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Get user by ID with cache-aside pattern.
    /// User data is read-heavy, making this an ideal cache candidate.
    /// </summary>
    public async Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.User.ById(id);

        try
        {
            var user = await _cache.GetOrCreateAsync(
                cacheKey,
                factory: async ct => await _context.Users.FirstOrDefaultAsync(u => u.Id == id, ct),
                cancellationToken: cancellationToken);

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache error for key {CacheKey}. Returning database result.", cacheKey);
            return await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        }
    }

    /// <summary>
    /// Get paginated users with cache-aside pattern.
    /// </summary>
    public async Task<(List<User> users, int totalCount)> GetUsersPageAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.User.Page(page, pageSize);

        try
        {
            var users = await _cache.GetOrCreateAsync(
                cacheKey,
                factory: async ct =>
                {
                    var skip = (page - 1) * pageSize;
                    return await _context.Users
                        .Skip(skip)
                        .Take(pageSize)
                        .ToListAsync(ct);
                },
                cancellationToken: cancellationToken);

            var totalCount = await _context.Users.CountAsync(cancellationToken);

            return (users ?? new List<User>(), totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache error for key {CacheKey}. Returning database result.", cacheKey);

            var skip = (page - 1) * pageSize;
            var users = await _context.Users
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var totalCount = await _context.Users.CountAsync(cancellationToken);

            return (users, totalCount);
        }
    }

    /// <summary>
    /// Invalidate user cache on update.
    /// </summary>
    public async Task InvalidateUserAsync(User user)
    {
        try
        {
            // Remove specific user key
            await _cache.RemoveAsync(CacheKeys.User.ById(user.Id));

            // Remove paginated lists
            for (int page = 1; page <= 100; page++)
            {
                await _cache.RemoveAsync(CacheKeys.User.Page(page, 50));
                if (page > 10) break;
            }

            _logger.LogDebug("Invalidated cache for user {UserId}", user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error invalidating cache for user {UserId}. Continuing.", user.Id);
        }
    }

    /// <summary>
    /// Invalidate all user-related cache entries.
    /// </summary>
    public async Task InvalidateAllUsersAsync()
    {
        try
        {
            _logger.LogInformation("Invalidating all user caches");

            for (int page = 1; page <= 100; page++)
            {
                await _cache.RemoveAsync(CacheKeys.User.Page(page, 50));
                if (page > 10) break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error invalidating all user caches");
        }
    }
}
