using ApexShop.API.Factories;
using ApexShop.API.StreamingResults;

namespace ApexShop.API.Extensions;

/// <summary>
/// Extension methods for HttpContext to support content negotiation.
/// Enables clean, fluent API for returning data in client-preferred formats.
/// Uses JSON source generators for optimal performance with JSON/NDJSON.
/// Priority: MessagePack > NDJSON > JSON (default)
///
/// Optimizations:
/// - Factory pattern for content negotiation (eliminates repeated header parsing)
/// - Configurable flush interval for latency/efficiency tradeoff
/// - Pre-computed format detection for better cache locality
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Streams a collection in the format specified by Accept header.
    /// Uses optimized factory pattern for content negotiation.
    /// ✅ FAST: Single header parse, optimal format selection
    /// </summary>
    /// <typeparam name="T">The type of items being streamed</typeparam>
    /// <param name="context">The HTTP context</param>
    /// <param name="data">The async enumerable data to stream</param>
    /// <param name="flushInterval">Records between flush operations (default: 100)</param>
    /// <returns>IResult with appropriate streaming format</returns>
    public static IResult StreamAs<T>(
        this HttpContext context,
        IAsyncEnumerable<T> data,
        int flushInterval = 100)
    {
        return StreamingResultFactory.Create(context, data, flushInterval);
    }

    /// <summary>
    /// Streams a collection in a specific format (bypasses Accept header negotiation).
    /// Use when you want explicit format control or Accept header is unavailable.
    /// </summary>
    public static IResult StreamAs<T>(
        this HttpContext context,
        IAsyncEnumerable<T> data,
        StreamFormat format,
        int flushInterval = 100)
    {
        return StreamingResultFactory.CreateByFormat(format, data, flushInterval);
    }

    /// <summary>
    /// Returns a single object in the format specified by Accept header.
    /// Uses JSON source generators for JSON format.
    /// </summary>
    /// <typeparam name="T">The type of object being returned</typeparam>
    /// <param name="context">The HTTP context</param>
    /// <param name="data">The object to return</param>
    /// <returns>IResult with appropriate format</returns>
    public static IResult RespondAs<T>(
        this HttpContext context,
        T data)
    {
        var accept = context.Request.Headers.Accept.ToString();

        // MessagePack
        if (accept.Contains("application/x-msgpack", StringComparison.OrdinalIgnoreCase))
            return new MessagePackResult<T>(data);

        // JSON (default)
        return Results.Json(data);
    }
}
