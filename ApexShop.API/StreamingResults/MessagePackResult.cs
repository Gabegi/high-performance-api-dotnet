using MessagePack;

namespace ApexShop.API.StreamingResults;

/// <summary>
/// Custom IResult implementation for returning a single object as MessagePack.
/// MessagePack is ~60% smaller and 5-10x faster to serialize than JSON.
/// Optimal for: high-bandwidth scenarios, binary protocol clients, performance-critical responses.
/// </summary>
public class MessagePackResult<T> : IResult
{
    private readonly T _data;

    public MessagePackResult(T data)
    {
        _data = data;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = "application/x-msgpack";
        httpContext.Response.Headers.CacheControl = "public, max-age=0";

        var cancellationToken = httpContext.RequestAborted;
        var stream = httpContext.Response.Body;

        try
        {
            // Serialize directly to response stream
            await MessagePackSerializer.SerializeAsync(
                stream,
                _data,
                cancellationToken: cancellationToken);

            await stream.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
    }
}
