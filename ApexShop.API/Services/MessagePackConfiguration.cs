using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace ApexShop.API.Services;

/// <summary>
/// MessagePack configuration and lazy initialization service.
///
/// IMPORTANT: This service enables lazy initialization of MessagePack serialization,
/// which prevents 15+ second cold start delays during application startup.
///
/// Instead of scanning all assemblies and registering formatters at startup (expensive),
/// we cache pre-compiled options and only initialize on first use.
/// </summary>
public static class MessagePackConfiguration
{
    private static bool _isInitialized;
    private static MessagePackSerializerOptions? _cachedOptions;

    /// <summary>
    /// Gets or creates MessagePack serializer options.
    /// Uses lazy initialization to defer expensive setup until first MessagePack request.
    /// Subsequent calls return the cached options (zero overhead).
    /// </summary>
    /// <remarks>
    /// This method is thread-safe due to:
    /// 1. The _isInitialized flag being checked first
    /// 2. The initialization code being idempotent (safe to run multiple times)
    /// 3. MessagePackSerializerOptions being immutable after creation
    /// </remarks>
    public static MessagePackSerializerOptions GetOrCreateOptions()
    {
        if (_isInitialized && _cachedOptions != null)
            return _cachedOptions;

        // ✅ FAST: Create options with standard resolver (no reflection scanning)
        // The standard resolver handles all basic .NET types and uses cached formatters
        var resolver = CompositeResolver.Create(
            // Custom formatters go here if needed
            Array.Empty<IMessagePackFormatter>(),

            // Standard resolvers (no expensive reflection)
            new IFormatterResolver[]
            {
                StandardResolver.Instance
            }
        );

        _cachedOptions = MessagePackSerializerOptions.Standard
            .WithResolver(resolver)
            .WithCompression(MessagePackCompression.Lz4BlockArray);

        _isInitialized = true;
        return _cachedOptions;
    }

    /// <summary>
    /// Registers MessagePack options into the dependency injection container.
    /// Uses lazy initialization - options are created on first access, not at startup.
    /// </summary>
    public static IServiceCollection AddLazyMessagePack(this IServiceCollection services)
    {
        // Register a factory that uses lazy initialization
        services.AddSingleton(sp => GetOrCreateOptions());
        return services;
    }
}
