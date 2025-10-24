using ApexShop.API.Endpoints.Categories;
using ApexShop.API.Endpoints.Orders;
using ApexShop.API.Endpoints.Products;
using ApexShop.API.Endpoints.Reviews;
using ApexShop.API.Endpoints.Users;
using ApexShop.API.JsonContext;
using ApexShop.Infrastructure;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.EnvironmentName);
builder.Services.AddScoped<DbSeeder>();

// Configure JSON serialization with source generators for improved performance and AOT support
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolver = ApexShopJsonContext.Default;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply migrations and seed database
// PERFORMANCE: Only run migrations/seeding in Development or when explicitly requested via env var
// In Production/Benchmarks, migrations should be pre-applied (via init container, CI/CD, etc.)
// Skipping this in Production eliminates 70-120ms cold start overhead
var runMigrations = app.Environment.IsDevelopment() ||
                    Environment.GetEnvironmentVariable("RUN_MIGRATIONS") == "true";
var runSeeding = app.Environment.IsDevelopment() ||
                 Environment.GetEnvironmentVariable("RUN_SEEDING") == "true";

if (runMigrations || runSeeding)
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (runMigrations)
        {
            await context.Database.MigrateAsync();
        }

        if (runSeeding)
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
            await seeder.SeedAsync();
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Health check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

// Map endpoints
app.MapProductEndpoints();
app.MapCategoryEndpoints();
app.MapUserEndpoints();
app.MapOrderEndpoints();
app.MapReviewEndpoints();

app.Run();

// Make Program accessible for WebApplicationFactory in tests/benchmarks
namespace ApexShop.API
{
    public partial class Program { }
}
