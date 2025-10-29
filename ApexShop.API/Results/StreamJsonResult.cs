using System.Text.Json;
using ApexShop.API.JsonContext;

namespace ApexShop.API.Results;

/// <summary>
/// Streams data as JSON format (objects separated by commas in an array-like stream).
/// Uses source-generated JSON serializers for optimal performance.
/// Optimal for: web browsers, standard JSON clients, maximum compatibility.
/// </summary>
public class StreamJsonResult<T> : IResult
{
    private readonly IAsyncEnumerable<T> _data;
    private readonly int _flushInterval;

    public StreamJsonResult(
        IAsyncEnumerable<T> data,
        int flushInterval = 100)
    {
        _data = data;
        _flushInterval = flushInterval;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = "application/json";
        httpContext.Response.Headers.CacheControl = "no-cache";

        var cancellationToken = httpContext.RequestAborted;
        var stream = httpContext.Response.Body;
        var itemCount = 0;
        var isFirst = true;

        try
        {
            // Write opening bracket for JSON array
            await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("["), cancellationToken);

            await foreach (var item in _data.WithCancellation(cancellationToken))
            {
                // Add comma separator (except for first item)
                if (!isFirst)
                {
                    await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(","), cancellationToken);
                }
                isFirst = false;

                // Serialize to JSON using source-generated context
                await JsonSerializer.SerializeAsync(
                    stream,
                    item,
                    ApexShopJsonContext.Default.GetTypeInfo(item.GetType()),
                    cancellationToken);

                // Flush periodically to send data immediately
                if (++itemCount % _flushInterval == 0)
                {
                    await stream.FlushAsync(cancellationToken);
                }
            }

            // Write closing bracket
            await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("]"), cancellationToken);

            // Final flush
            await stream.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected - gracefully handle
        }
    }
}
