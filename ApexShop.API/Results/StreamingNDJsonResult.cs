using System.Text;
using System.Text.Json;
using ApexShop.API.JsonContext;

namespace ApexShop.API.Results;

/// <summary>
/// Streams data as NDJSON (Newline Delimited JSON) format.
/// Each object is a complete JSON document, separated by newlines.
/// Optimal for: text-based streaming, log aggregation, downstream processing.
/// </summary>
public class StreamingNDJsonResult<T> : IResult
{
    private readonly IAsyncEnumerable<T> _data;
    private readonly int _flushInterval;

    public StreamingNDJsonResult(
        IAsyncEnumerable<T> data,
        int flushInterval = 100)
    {
        _data = data;
        _flushInterval = flushInterval;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = "application/x-ndjson";
        httpContext.Response.Headers.CacheControl = "no-cache";

        var cancellationToken = httpContext.RequestAborted;
        var stream = httpContext.Response.Body;
        var itemCount = 0;

        try
        {
            await foreach (var item in _data.WithCancellation(cancellationToken))
            {
                // Serialize to JSON using source-generated context
                await JsonSerializer.SerializeAsync(
                    stream,
                    item,
                    ApexShopJsonContext.Default.GetTypeInfo(item.GetType()),
                    cancellationToken);

                // Write newline separator
                await stream.WriteAsync(Encoding.UTF8.GetBytes("\n"), cancellationToken);

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
        }
    }
}
