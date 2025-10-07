using ApexShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApexShop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string environmentName)
    {
        // Database
        services.AddDbContext<AppDbContext>(options =>
        {
            var npgsqlOptionsBuilder = options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    // Enable automatic retry on transient failures (network issues, deadlocks, etc.)
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);

                    // Set explicit command timeout
                    npgsqlOptions.CommandTimeout(30);
                })
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking); // Disable change tracking by default for read-heavy API

            // Enable SQL logging in Development only
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
        });

        return services;
    }
}
