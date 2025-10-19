using ApexShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace ApexShop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string environmentName)
    {
        // Fix Npgsql DateTime UTC handling - allow UTC DateTimes with timestamp without time zone
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        // Build connection string with proper pooling configuration
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var connectionStringBuilder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
        {
            // Connection Pool Configuration for High Performance
            MaxPoolSize = 200,              // Maximum connections in pool (up from default 100)
            MinPoolSize = 10,               // Keep 10 connections warm for fast response
            ConnectionIdleLifetime = 300,   // Close idle connections after 5 minutes
            ConnectionPruningInterval = 10, // Check for idle connections every 10 seconds
            Timeout = 30,                   // Connection timeout (seconds)
            CommandTimeout = 60,            // Command timeout (seconds) - increased for complex queries
            Pooling = true,                 // Enable connection pooling
            NoResetOnClose = false          // Reset connection state when returned to pool
        };

        // Database - using DbContext pooling for better performance
        services.AddDbContextPool<AppDbContext>(options =>
        {
            var npgsqlOptionsBuilder = options.UseNpgsql(
                connectionStringBuilder.ConnectionString,
                npgsqlOptions =>
                {
                    // Retry disabled (maxRetryCount: 0) to enable true streaming without internal buffering
                    // When retry is enabled, EF Core buffers all query results before streaming
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 0,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: new[] {
                            "57P03", // cannot_connect_now - server is starting up
                            "53300", // too_many_connections
                            "53400"  // configuration_limit_exceeded
                        });

                    // Set explicit command timeout (aligned with connection string)
                    npgsqlOptions.CommandTimeout(60);
                })
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking) // Disable change tracking by default for read-heavy API
            .UseModel(CompiledModels.AppDbContextModel.Instance); // PERFORMANCE: Use precompiled model for 100-150ms faster cold start

            // Enable detailed logging in Development only to minimize production overhead
            if (environmentName == "Development")
            {
                npgsqlOptionsBuilder
                    .EnableSensitiveDataLogging()  // Shows parameter values in logs
                    .EnableDetailedErrors()        // More detailed exception messages
                    .LogTo(
                        Console.WriteLine,
                        new[] { DbLoggerCategory.Database.Command.Name },
                        LogLevel.Information);
            }
            else
            {
                // Production: Only log errors and critical issues
                npgsqlOptionsBuilder
                    .LogTo(
                        Console.WriteLine,
                        new[] { DbLoggerCategory.Database.Command.Name },
                        LogLevel.Error);
            }
        },
        poolSize: 512); // Increased DbContext pool size for high concurrency (default: 128)

        // Health Checks - Database connectivity monitoring
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(
                name: "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "db", "postgresql", "ready", "live" });

        return services;
    }
}
