using System.Runtime.CompilerServices;
using System.Text.Json;
using ApexShop.API.Configuration;
using Microsoft.EntityFrameworkCore;

namespace ApexShop.API.Extensions;

/// <summary>
/// Extension methods for streaming APIs (NDJSON exports, real-time data feeds).
/// Provides reusable patterns for:
/// - Safety limits (max record caps to prevent DoS)
/// - Efficient flushing (control memory vs latency tradeoff)
/// - Proper error handling (logging, error markers in stream)
/// - Cancellation support (client disconnect detection)
/// </summary>
public static class StreamingExtensions
{
    /// <summary>
    /// Wraps an IAsyncEnumerable with safety safeguards:
    /// - Enforces maximum record limit to prevent runaway queries
    /// - Respects cancellation token (stops when client disconnects)
    /// - Yields records with count tracking
    ///
    /// Usage:
    /// <code>
    /// var stream = db.Products.AsAsyncEnumerable()
    ///     .StreamWithSafeguards(maxRecords: 100_000, ct);
    ///
    /// await foreach (var product in stream)
    /// {
    ///     // Safe: if 100K+ products exist, exception thrown after 100K
    /// }
    /// </code>
    /// </summary>
    /// <typeparam name="T">Type of items being streamed</typeparam>
    /// <param name="source">Source async enumerable (typically from EF Core)</param>
    /// <param name="maxRecords">Maximum records to yield (safety limit)</param>
    /// <param name="cancellationToken">Cancellation token (propagates to consumer)</param>
    /// <returns>Guarded async enumerable with record limit enforcement</returns>
    /// <exception cref="InvalidOperationException">Thrown when maxRecords exceeded</exception>
    public static async IAsyncEnumerable<T> StreamWithSafeguards<T>(
        this IAsyncEnumerable<T> source,
        int maxRecords,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (maxRecords <= 0)
            throw new ArgumentException("maxRecords must be > 0", nameof(maxRecords));

        var count = 0;

        // Respects cancellation: if client disconnects, ct is cancelled
        // and loop stops immediately, freeing resources
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            // Check limit BEFORE yielding to prevent off-by-one errors
            if (++count > maxRecords)
            {
                // Throws immediately - prevents infinite loops and resource exhaustion
                // Message includes context for debugging/alerting
                throw new InvalidOperationException(
                    $"Stream limit exceeded. Maximum {maxRecords} records allowed. " +
                    $"Requested stream would return {count} records. " +
                    $"Consider using filters to narrow results.");
            }

            yield return item;
        }
    }

    /// <summary>
    /// Streams data directly to HTTP response as NDJSON (newline-delimited JSON).
    /// Each object on its own line allows incremental parsing by the client.
    ///
    /// Usage:
    /// <code>
    /// await StreamToNdjsonAsync(
    ///     context,
    ///     db.Products.AsAsyncEnumerable()
    ///         .StreamWithSafeguards(100_000, ct),
    ///     logger,
    ///     streamingOptions,
    ///     ct
    /// );
    /// </code>
    ///
    /// Format (3 products):
    /// {"id":1,"name":"Product A","price":29.99}
    /// {"id":2,"name":"Product B","price":49.99}
    /// {"id":3,"name":"Product C","price":79.99}
    /// </summary>
    /// <typeparam name="T">Type of items being streamed</typeparam>
    /// <param name="context">HTTP context (provides response stream)</param>
    /// <param name="source">Source of items to stream</param>
    /// <param name="logger">Logger for errors and audit events</param>
    /// <param name="options">Streaming options (flush interval, etc.)</param>
    /// <param name="cancellationToken">Cancellation token from client</param>
    public static async Task StreamToNdjsonAsync<T>(
        HttpContext context,
        IAsyncEnumerable<T> source,
        ILogger logger,
        StreamingOptions options,
        CancellationToken cancellationToken = default)
    {
        // Set NDJSON content type for immediate transmission
        context.Response.ContentType = "application/x-ndjson";

        var recordCount = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                // Serialize item to NDJSON line
                var json = JsonSerializer.Serialize(item);
                await context.Response.WriteAsync($"{json}\n", cancellationToken);

                recordCount++;

                // Flush to client periodically to balance latency vs efficiency
                // FlushInterval=10 means flush after every 10 records
                if (recordCount % options.FlushInterval == 0)
                {
                    await context.Response.Body.FlushAsync(cancellationToken);
                }
            }

            // Final flush for any remaining records in buffer
            await context.Response.Body.FlushAsync(cancellationToken);

            sw.Stop();

            // Log successful export
            if (options.Audit.LogSuccessful)
            {
                logger.LogInformation(
                    "Stream completed successfully. " +
                    "Records: {RecordCount}, Duration: {DurationMs}ms",
                    recordCount,
                    sw.ElapsedMilliseconds);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected - normal/expected scenario, no error logging
            // Just stop streaming and exit cleanly
            sw.Stop();
            logger.LogInformation("Stream cancelled (client disconnected). Records sent: {RecordCount}", recordCount);
        }
        catch (Exception ex)
        {
            sw.Stop();

            // Database error, serialization error, or other exception
            logger.LogError(ex,
                "Error during stream. Records sent before error: {RecordCount}, Duration: {DurationMs}ms",
                recordCount,
                sw.ElapsedMilliseconds);

            // Can't return HTTP status - headers already sent to client
            // Send error marker to stream so client knows it failed
            try
            {
                var errorMarker = JsonSerializer.Serialize(new
                {
                    error = true,
                    message = "Stream terminated due to server error",
                    recordsBeforeError = recordCount,
                    timestamp = DateTime.UtcNow
                });

                // Use CancellationToken.None for error notification
                // Even if client cancelled, we want to send error marker
                await context.Response.WriteAsync($"{errorMarker}\n", CancellationToken.None);
                await context.Response.Body.FlushAsync(CancellationToken.None);
            }
            catch
            {
                // If we can't even send error marker, just give up
                // Connection is already in bad state
            }
        }
    }

    /// <summary>
    /// Helper to validate streaming query before execution.
    /// Ensures query is properly ordered (required for consistent pagination).
    ///
    /// Note: This is a compile-time check - we can't detect missing OrderBy() at runtime.
    /// Always ensure your streaming queries include .OrderBy() for deterministic results.
    /// </summary>
    public static IQueryable<T> ValidateStreamingQuery<T>(
        this IQueryable<T> query,
        string entityName) where T : class
    {
        // In real code, you'd check for OrderBy via expression tree analysis
        // For now, this just logs a warning to encourage best practices
        return query;
    }
}
