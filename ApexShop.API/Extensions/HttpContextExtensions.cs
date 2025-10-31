using ApexShop.API.Results;

namespace ApexShop.API.Extensions;

/// <summary>
/// Extension methods for HttpContext to support content negotiation.
/// Enables clean, fluent API for returning data in client-preferred formats.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Returns streaming data in format specified by Accept header.
    /// Priority: MessagePack > NDJSON > JSON (default)
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

        if (accept.Contains("application/x-msgpack", StringComparison.OrdinalIgnoreCase))
            return new StreamingMessagePackResult<T>(data);

        if (accept.Contains("application/x-ndjson", StringComparison.OrdinalIgnoreCase))
            return new StreamingNDJsonResult<T>(data);

        return new StreamJsonResult<T>(data);
    }

    /// <summary>
    /// Returns streaming data with custom flush interval in format specified by Accept header.
    /// </summary>
    /// <typeparam name="T">The type of items being streamed</typeparam>
    /// <param name="context">The HTTP context</param>
    /// <param name="data">The async enumerable data to stream</param>
    /// <param name="flushInterval">How many items between flush operations</param>
    /// <returns>IResult with appropriate streaming format</returns>
    public static IResult StreamAs<T>(
        this HttpContext context,
        IAsyncEnumerable<T> data,
        int flushInterval)
    {
        var accept = context.Request.Headers.Accept.ToString();

        if (accept.Contains("application/x-msgpack", StringComparison.OrdinalIgnoreCase))
            return new StreamingMessagePackResult<T>(data, flushInterval);

        if (accept.Contains("application/x-ndjson", StringComparison.OrdinalIgnoreCase))
            return new StreamingNDJsonResult<T>(data, flushInterval);

        return new StreamJsonResult<T>(data, flushInterval);
    }
}
