using ApexShop.API.Endpoints.Categories;
using ApexShop.API.Endpoints.Orders;
using ApexShop.API.Endpoints.Products;
using ApexShop.API.Endpoints.Reviews;
using ApexShop.API.Endpoints.Users;
using ApexShop.API.JsonContext;
using ApexShop.Infrastructure;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
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

// Output caching for high-performance APIs
// Caches GET responses for paginated and single-item endpoints
// ⚠️ Do NOT cache streaming endpoints - they handle memory efficiently already
builder.Services.AddOutputCache(options =>
{
    // Policy for paginated list endpoints
    options.AddPolicy("Lists", policy => policy
        .Expire(TimeSpan.FromMinutes(10))
        .Tag("lists"));

    // Policy for single item endpoints (GetById, GetBySlug, etc.)
    options.AddPolicy("Single", policy => policy
        .Expire(TimeSpan.FromMinutes(15))
        .Tag("single"));
});

// Response compression for reduced payload sizes
// Automatically compresses responses using Brotli (primary) and Gzip (fallback)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true; // Safe with modern TLS
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();

    // Extend defaults rather than replace (maintains compatibility with future versions)
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "application/json",
        "application/x-ndjson",
        "image/svg+xml"  // SVG is text-based XML, benefits greatly from compression
    });
});

// Configure compression levels for optimal performance
// "Fastest" prioritizes speed over compression ratio
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

// Configure Kestrel for HTTP/3 support (via code - Kestrel will also read from appsettings.json)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(443, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2AndHttp3;
        listenOptions.UseHttps();
    });
});

var app = builder.Build();

// Advertise HTTP/3 support via Alt-Svc header
app.Use(async (context, next) =>
{
    context.Response.Headers.AltSvc = "h3=\":443\"; ma=86400";
    await next();
});

// Enable response compression middleware (applies to all responses)
// Automatically negotiates Brotli or Gzip based on Accept-Encoding header
// Reduces payload sizes by 60-80% for JSON, 40-70% for streaming responses
app.UseResponseCompression();

// Enable output caching middleware (must come before endpoints)
app.UseOutputCache();

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
