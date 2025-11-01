using ApexShop.API.StreamingResults;

namespace ApexShop.API.Extensions;

/// <summary>
/// Extension methods for HttpContext to support content negotiation.
/// Enables clean, fluent API for returning data in client-preferred formats.
/// Uses JSON source generators for optimal performance with JSON/NDJSON.
/// Priority: MessagePack > NDJSON > JSON (default)
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Streams a collection in the format specified by Accept header.
    /// Uses JSON source generators for JSON/NDJSON formats.
    /// </summary>
    /// <typeparam name="T">The type of items being streamed</typeparam>
    /// <param name="context">The HTTP context</param>
    /// <param name="data">The async enumerable data to stream</param>
    /// <returns>IResult with appropriate streaming format</returns>
    public static IResult StreamAs<T>(
        this HttpContext context,
        IAsyncEnumerable<T> data)
    {
        var accept = context.Request.Headers.Accept.ToString();

        // MessagePack - highest priority (smallest, fastest)
        if (accept.Contains("application/x-msgpack", StringComparison.OrdinalIgnoreCase))
            return new StreamingMessagePackResult<T>(data);

        // NDJSON - for streaming clients
        if (accept.Contains("application/x-ndjson", StringComparison.OrdinalIgnoreCase))
            return new StreamingNDJsonResult<T>(data);

        // JSON Array - default (most compatible)
        return new StreamJsonResult<T>(data);
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
