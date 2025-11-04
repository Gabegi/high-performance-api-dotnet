namespace ApexShop.API.Caching;

/// <summary>
/// Centralized cache key conventions to prevent typos and ensure consistency.
///
/// Key Format: "ApexShop:{Environment}:{EntityType}:{Id}"
/// Examples:
///   - Single item: "ApexShop:Prod:Product:123"
///   - List by filter: "ApexShop:Prod:Products:Category:Electronics"
///   - Aggregation: "ApexShop:Prod:CategoryStats:Top10"
///
/// SECURITY GUIDELINES:
/// ✅ Cache: Products, Categories, Orders (non-sensitive, read-heavy)
/// ❌ Do NOT Cache: Users (PII), Auth tokens, Passwords, Sensitive personal data
/// </summary>
public static class CacheKeys
{
    private static readonly string _env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
    private static readonly string _prefix = $"ApexShop:{_env}";

    /// <summary>
    /// Product cache keys
    ///
    /// Tag-based invalidation (Redis): One RemoveByTagAsync() removes ALL entries with that tag
    /// Much more efficient than looping through pages manually
    /// </summary>
    public static class Product
    {
        /// <summary>
        /// Single product by ID: "ApexShop:Prod:Product:{id}"
        /// </summary>
        public static string ById(int id) => $"{_prefix}:Product:{id}";

        /// <summary>
        /// Products by category: "ApexShop:Prod:Products:Category:{categoryId}"
        /// </summary>
        public static string ByCategory(int categoryId) => $"{_prefix}:Products:Category:{categoryId}";

        /// <summary>
        /// All products paginated: "ApexShop:Prod:Products:Page:{page}:{pageSize}"
        /// </summary>
        public static string Page(int page, int pageSize) => $"{_prefix}:Products:Page:{page}:{pageSize}";

        /// <summary>
        /// Tag for all product-related cache entries
        /// Used for atomic bulk invalidation: RemoveByTagAsync("product") removes ALL products
        /// </summary>
        public const string Tag = "product";

        /// <summary>
        /// Tag for category-related product cache entries
        /// Used for invalidating products in specific category without affecting other categories
        /// </summary>
        public static string CategoryTag(int categoryId) => $"product-category-{categoryId}";
    }

    /// <summary>
    /// Category cache keys
    /// </summary>
    public static class Category
    {
        /// <summary>
        /// Single category by ID: "ApexShop:Prod:Category:{id}"
        /// </summary>
        public static string ById(int id) => $"{_prefix}:Category:{id}";

        /// <summary>
        /// All categories: "ApexShop:Prod:Categories"
        /// </summary>
        public static string All => $"{_prefix}:Categories";

        /// <summary>
        /// Tag for all category-related cache entries
        /// </summary>
        public const string Tag = "category";
    }

    /// <summary>
    /// User cache keys
    /// </summary>
    public static class User
    {
        /// <summary>
        /// Single user by ID: "ApexShop:Prod:User:{id}"
        /// </summary>
        public static string ById(int id) => $"{_prefix}:User:{id}";

        /// <summary>
        /// All users paginated: "ApexShop:Prod:Users:Page:{page}:{pageSize}"
        /// </summary>
        public static string Page(int page, int pageSize) => $"{_prefix}:Users:Page:{page}:{pageSize}";

        /// <summary>
        /// Tag for all user-related cache entries
        /// </summary>
        public const string Tag = "user";
    }

    /// <summary>
    /// Order cache keys
    /// </summary>
    public static class Order
    {
        /// <summary>
        /// Single order by ID: "ApexShop:Prod:Order:{id}"
        /// </summary>
        public static string ById(int id) => $"{_prefix}:Order:{id}";

        /// <summary>
        /// All orders paginated: "ApexShop:Prod:Orders:Page:{page}:{pageSize}"
        /// </summary>
        public static string Page(int page, int pageSize) => $"{_prefix}:Orders:Page:{page}:{pageSize}";

        /// <summary>
        /// Orders by user: "ApexShop:Prod:Orders:User:{userId}"
        /// </summary>
        public static string ByUser(int userId) => $"{_prefix}:Orders:User:{userId}";

        /// <summary>
        /// Tag for all order-related cache entries
        /// </summary>
        public const string Tag = "order";

        /// <summary>
        /// Tag for user-specific orders
        /// </summary>
        public static string UserTag(int userId) => $"order-user-{userId}";
    }

    /// <summary>
    /// Review cache keys
    /// </summary>
    public static class Review
    {
        /// <summary>
        /// Single review by ID: "ApexShop:Prod:Review:{id}"
        /// </summary>
        public static string ById(int id) => $"{_prefix}:Review:{id}";

        /// <summary>
        /// All reviews paginated: "ApexShop:Prod:Reviews:Page:{page}:{pageSize}"
        /// </summary>
        public static string Page(int page, int pageSize) => $"{_prefix}:Reviews:Page:{page}:{pageSize}";

        /// <summary>
        /// Reviews for product: "ApexShop:Prod:Reviews:Product:{productId}"
        /// </summary>
        public static string ByProduct(int productId) => $"{_prefix}:Reviews:Product:{productId}";

        /// <summary>
        /// Tag for all review-related cache entries
        /// </summary>
        public const string Tag = "review";

        /// <summary>
        /// Tag for product-specific reviews
        /// </summary>
        public static string ProductTag(int productId) => $"review-product-{productId}";
    }
}
