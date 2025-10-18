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

        // Database - using DbContext pooling for better performance
        services.AddDbContextPool<AppDbContext>(options =>
        {
            var npgsqlOptionsBuilder = options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
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

                    // Set explicit command timeout
                    npgsqlOptions.CommandTimeout(30);
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
        });

        // Health Checks - Database connectivity monitoring
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(
                name: "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "db", "postgresql", "ready", "live" });

        return services;
    }
}
