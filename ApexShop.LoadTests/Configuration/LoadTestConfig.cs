namespace ApexShop.LoadTests.Configuration;

/// <summary>
/// Configuration for ApexShop API load tests.
///
/// LOAD TEST STRATEGY: Products-Only Testing
/// - All entities (Products, Orders, Categories, Reviews, Users) use identical patterns
/// - Products has the most complex operations (filters, pagination, bulk, streaming)
/// - Testing Products thoroughly validates entire infrastructure performance
///
/// See README.md for detailed load testing documentation.
/// </summary>
public static class LoadTestConfig
{
    /// <summary>
    /// Base URL for the API under test.
    ///
    /// Default: http://localhost:5193 (Development Kestrel port)
    ///
    /// Override via environment variable for different environments:
    ///   Development:  API_BASE_URL=http://localhost:5193
    ///   Staging:      API_BASE_URL=https://staging-api.example.com
    ///   Production:   API_BASE_URL=https://api.example.com
    ///
    /// Set via environment variables:
    ///   Windows:    $env:API_BASE_URL="http://localhost:5000"
    ///   Linux/Mac:  export API_BASE_URL="http://localhost:5000"
    /// </summary>
    public static string BaseUrl =>
        Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:5193";

    /// <summary>
    /// HTTP client timeout for all requests.
    ///
    /// Set to 90 seconds to accommodate:
    /// - Stress test scenarios with high server load
    /// - Multi-step workflows (browse → view → create)
    /// - Database-heavy operations under load
    /// - Network latency in staging/production environments
    ///
    /// Normal requests complete in <100ms, but under stress can take 5-10s.
    /// </summary>
    public static TimeSpan RequestTimeout { get; } = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Maximum response content buffer size: 10MB.
    ///
    /// Rationale:
    /// - Products list endpoint returns ~50KB for 50 items (default page)
    /// - Streaming endpoints can return larger payloads
    /// - 10MB provides headroom for future growth
    /// - Prevents memory issues from accidentally unbounded responses
    /// </summary>
    public static int MaxResponseBufferSize => 10_000_000;

    /// <summary>
    /// Expected data ranges from seeded database.
    ///
    /// IMPORTANT: These must match your seeding logic in ApexShop.API/Data/DataSeeder.cs
    /// If seeding changes, update these values accordingly.
    ///
    /// Note: Only Products entity is load tested, as all entities share identical
    /// infrastructure patterns (EF Core, OutputCache, Minimal APIs, serialization).
    /// </summary>
    public static class DataRanges
    {
        /// <summary>
        /// Maximum Product ID in seeded database: 15,000 products.
        ///
        /// Used for random product lookups in GetProductById load tests.
        /// Range: 1 to 15000 (inclusive)
        ///
        /// Note: IDs may have gaps due to benchmark runs creating/deleting products.
        /// Load tests handle 404 responses gracefully.
        /// </summary>
        public static int MaxProductId => 15000;

        /// <summary>
        /// Maximum Category ID in seeded database: 15 categories.
        ///
        /// Used when creating test products (CategoryId is a required foreign key).
        /// Range: 1 to 15 (inclusive)
        ///
        /// Categories: Electronics, Clothing, Books, Home & Garden, Sports, Toys, etc.
        /// </summary>
        public static int MaxCategoryId => 15;
    }

    /// <summary>
    /// Load test intensity profiles.
    ///
    /// These are reference values - actual load is defined in scenario classes.
    /// Use these for consistency across different test scenarios.
    /// </summary>
    public static class LoadProfiles
    {
        /// <summary>
        /// Light load: Smoke test to verify API is functional.
        /// Suitable for: CI/CD pipelines, pre-deployment checks.
        /// </summary>
        public static int SmokeTestRps => 5;

        /// <summary>
        /// Normal load: Expected production baseline.
        /// Suitable for: Performance baseline measurement.
        /// </summary>
        public static int BaselineRps => 10;

        /// <summary>
        /// Moderate load: Above-average traffic.
        /// Suitable for: Capacity planning, SLA verification.
        /// </summary>
        public static int ModerateRps => 20;

        /// <summary>
        /// High load: Peak traffic simulation.
        /// Suitable for: Stress testing, finding breaking points.
        /// </summary>
        public static int HighRps => 50;

        /// <summary>
        /// Spike load: Burst traffic (e.g., flash sale, viral post).
        /// Suitable for: Spike testing, circuit breaker validation.
        /// </summary>
        public static int SpikeRps => 100;
    }
}
