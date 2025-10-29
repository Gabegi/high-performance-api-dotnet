using MessagePack;

namespace ApexShop.API.Results;

/// <summary>
/// Streams data as MessagePack format without length prefixes.
/// MessagePack is self-delimiting - each object contains its own boundaries.
/// ~60% smaller than JSON, 5-10x faster serialization.
/// </summary>
public class StreamingMessagePackResult<T> : IResult
{
    private readonly IAsyncEnumerable<T> _data;
    private readonly int _flushInterval;

    public StreamingMessagePackResult(
        IAsyncEnumerable<T> data,
        int flushInterval = 100)
    {
        _data = data;
        _flushInterval = flushInterval;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = "application/x-msgpack";
        httpContext.Response.Headers.CacheControl = "no-cache";

        var cancellationToken = httpContext.RequestAborted;
        var stream = httpContext.Response.Body;
        var itemCount = 0;

        try
        {
            await foreach (var item in _data.WithCancellation(cancellationToken))
            {
                // Serialize directly to stream - zero allocation
                await MessagePackSerializer.SerializeAsync(
                    stream,
                    item,
                    cancellationToken: cancellationToken);

                // Flush periodically to send data immediately
                if (++itemCount % _flushInterval == 0)
                {
                    await stream.FlushAsync(cancellationToken);
                }
            }

            // Final flush
            await stream.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected - gracefully handle
            // No need to abort, cancellation is normal
        }
    }
}
