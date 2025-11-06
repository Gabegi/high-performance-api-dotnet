namespace ApexShop.API.Configuration;

/// <summary>
/// Configuration options for streaming endpoints (NDJSON export, real-time data feeds, etc.)
/// Provides safety limits, performance tuning, and rate limiting settings.
/// </summary>
public class StreamingOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Streaming";

    /// <summary>
    /// Maximum number of records allowed in a single stream request.
    /// Prevents DoS attacks, runaway queries, and server resource exhaustion.
    ///
    /// Typical values:
    /// - 10,000: Conservative (smallest datasets)
    /// - 50,000: Moderate (typical exports)
    /// - 100,000: Permissive (large datasets)
    /// - 1,000,000: Very permissive (entire database exports)
    ///
    /// Example: If MaxRecords=100000 and client requests all 150K products,
    /// stream will yield 100K products then throw InvalidOperationException.
    /// </summary>
    public int MaxRecords { get; set; } = 100_000;

    /// <summary>
    /// Number of records between flush operations (flushing sends buffered data to client).
    ///
    /// Effects:
    /// - Lower values (1-10): More frequent flushes, lower latency, higher CPU/network overhead
    /// - Higher values (100-1000): Fewer flushes, lower CPU, higher client latency waiting for data
    /// - Recommended: 10-50 for balance between latency and efficiency
    ///
    /// Example: If FlushInterval=10, stream flushes to client after every 10 records yielded.
    /// </summary>
    public int FlushInterval { get; set; } = 10;

    /// <summary>
    /// Rate limiting configuration for streaming endpoints (exports, real-time feeds).
    /// Prevents API abuse and ensures fair resource allocation across clients.
    /// </summary>
    public RateLimitOptions RateLimit { get; set; } = new();

    /// <summary>
    /// Audit logging configuration for tracking who accessed what data and when.
    /// Useful for compliance, security monitoring, and data access audits.
    /// </summary>
    public AuditOptions Audit { get; set; } = new();

    /// <summary>
    /// Validates that configuration values are within acceptable ranges.
    /// </summary>
    public void Validate()
    {
        if (MaxRecords <= 0)
            throw new ArgumentException("MaxRecords must be > 0", nameof(MaxRecords));

        if (FlushInterval <= 0)
            throw new ArgumentException("FlushInterval must be > 0", nameof(FlushInterval));

        if (MaxRecords < FlushInterval)
            throw new ArgumentException("FlushInterval cannot exceed MaxRecords", nameof(FlushInterval));

        RateLimit.Validate();
        Audit.Validate();
    }
}

/// <summary>
/// Rate limiting configuration for streaming endpoints.
/// Uses fixed window algorithm: X requests per Y time window.
/// </summary>
public class RateLimitOptions
{
    /// <summary>
    /// Policy name used in endpoint configuration.
    /// Example: .RequireRateLimiting("streaming")
    /// </summary>
    public string PolicyName { get; set; } = "streaming";

    /// <summary>
    /// Maximum number of streaming requests allowed per window.
    ///
    /// Typical values:
    /// - 1-2: Very strict (for large exports)
    /// - 5-10: Moderate (standard exports)
    /// - 20+: Permissive (frequent exports)
    ///
    /// Example: PermitLimit=5 means max 5 export requests per WindowMinutes.
    /// 6th request within window receives 429 Too Many Requests.
    /// </summary>
    public int PermitLimit { get; set; } = 5;

    /// <summary>
    /// Time window duration in minutes for rate limiting.
    ///
    /// Example: If PermitLimit=5 and WindowMinutes=1:
    /// - 00:00 - Client can make 5 requests
    /// - 00:01 - Window resets, client can make 5 more requests
    ///
    /// Typical values: 1, 5, 15, 60 minutes
    /// </summary>
    public int WindowMinutes { get; set; } = 1;

    /// <summary>
    /// Whether to queue requests that exceed the limit (FIFO queue).
    /// - true: Requests wait in queue, served when limit window resets
    /// - false: Requests immediately rejected with 429
    ///
    /// Recommendation: false for streaming (avoid client timeout while waiting in queue)
    /// </summary>
    public bool EnableQueueing { get; set; } = false;

    /// <summary>
    /// Maximum queue depth if QueueingEnabled (prevents queue from growing unbounded).
    /// Ignored if EnableQueueing=false.
    /// </summary>
    public int MaxQueueLength { get; set; } = 0;

    /// <summary>
    /// Partitioning strategy for rate limit keys.
    /// - "userId": Limit per authenticated user (multi-tenant, most fair)
    /// - "apiKey": Limit per API key (for service-to-service)
    /// - "ipAddress": Limit per IP (simple, but fails for NAT/proxies)
    ///
    /// Recommendation: "userId" if authentication available, else "ipAddress"
    /// </summary>
    public string PartitionKey { get; set; } = "userId";

    /// <summary>
    /// Validates configuration values.
    /// </summary>
    public void Validate()
    {
        if (PermitLimit <= 0)
            throw new ArgumentException("PermitLimit must be > 0", nameof(PermitLimit));

        if (WindowMinutes <= 0)
            throw new ArgumentException("WindowMinutes must be > 0", nameof(WindowMinutes));

        if (MaxQueueLength < 0)
            throw new ArgumentException("MaxQueueLength must be >= 0", nameof(MaxQueueLength));
    }
}

/// <summary>
/// Audit logging configuration for streaming data access.
/// Tracks who accessed what data, when, and how much.
/// </summary>
public class AuditOptions
{
    /// <summary>
    /// Whether to enable audit logging for streaming endpoints.
    /// - true: Log all exports (user, timestamp, filters, record count)
    /// - false: No logging (recommended for high-volume scenarios)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to include detailed filter parameters in audit logs.
    /// - true: Log category=Electronics&minPrice=100 (useful for audits)
    /// - false: Log generic "export requested" (privacy-friendly)
    ///
    /// Recommendation: true for compliance/regulatory requirements
    /// </summary>
    public bool LogDetailedFilters { get; set; } = true;

    /// <summary>
    /// Whether to log successful exports (export completed event).
    /// - true: Logs include record count, duration, success status
    /// - false: Only logs failures
    /// </summary>
    public bool LogSuccessful { get; set; } = true;

    /// <summary>
    /// Logging level for streaming operations.
    /// - "Information": Standard logging
    /// - "Debug": Verbose (includes internal buffer/flush operations)
    /// - "Warning": Only warnings and errors
    /// </summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>
    /// Validates configuration values.
    /// </summary>
    public void Validate()
    {
        if (!string.IsNullOrEmpty(LogLevel) &&
            !new[] { "Debug", "Information", "Warning", "Error", "Critical" }.Contains(LogLevel))
        {
            throw new ArgumentException("Invalid LogLevel value", nameof(LogLevel));
        }
    }
}
