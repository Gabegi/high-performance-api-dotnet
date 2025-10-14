namespace ApexShop.LoadTests.Configuration;

public static class LoadTestConfig
{
    /// <summary>
    /// Base URL for the API under test.
    /// Can be overridden via environment variable API_BASE_URL.
    /// </summary>
    public static string BaseUrl => Environment.GetEnvironmentVariable("API_BASE_URL")
                                    ?? "http://localhost:5193";

    /// <summary>
    /// HTTP client timeout for all requests.
    /// </summary>
    public static TimeSpan RequestTimeout => TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum response content buffer size (10MB).
    /// </summary>
    public static int MaxResponseBufferSize => 10_000_000;

    /// <summary>
    /// Expected data ranges (based on seeded data).
    /// </summary>
    public static class DataRanges
    {
        public static int MaxProductId => 15000;
        public static int MaxUserId => 3000;
        public static int MaxCategoryId => 15;
    }
}
