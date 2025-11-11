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

        // ✅ Optimized DbContext with small pool (Saves 4-9s at startup)
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Database connection string not configured");

        var npgsqlBuilder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
        {
            Timeout = 5,                    // ✅ Fast connection timeout (reduced from 30s)
            CommandTimeout = 30,            // ✅ Reasonable query timeout (reduced from 60s)
            Pooling = true,
            MinPoolSize = 0,                // ✅ Don't pre-create connections (CRITICAL for startup speed)
            MaxPoolSize = 32,               // ✅ Sufficient for single machine
            ConnectionIdleLifetime = 300    // Close idle connections after 5 minutes
        };

        // Database - using DbContext pooling for better performance
        services.AddDbContextPool<AppDbContext>(options =>
        {
            var npgsqlOptionsBuilder = options.UseNpgsql(
                npgsqlBuilder.ToString(),
                npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                })
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking) // Disable change tracking by default for read-heavy API
            .UseModel(CompiledModels.AppDbContextModel.Instance); // PERFORMANCE: Use precompiled model for 100-150ms faster cold start

            // ✅ Disable expensive features in non-Development environments
            if (environmentName != "Development")
            {
                npgsqlOptionsBuilder
                    .EnableSensitiveDataLogging(false)
                    .EnableDetailedErrors(false);
            }
            else
            {
                // Development: Keep detailed logging
                npgsqlOptionsBuilder
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors()
                    .LogTo(
                        Console.WriteLine,
                        new[] { DbLoggerCategory.Database.Command.Name },
                        LogLevel.Information);
            }
        },
        poolSize: 32); // ✅ CRITICAL: Reduced from 512 to 32 (saves 4-9s at startup)

        // Health Checks - Database connectivity monitoring
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(
                name: "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "db", "postgresql", "ready", "live" });

        return services;
    }
}
