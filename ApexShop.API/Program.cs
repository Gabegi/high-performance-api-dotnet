using ApexShop.API.Caching;
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
using Microsoft.Extensions.Caching.Hybrid;
using StackExchange.Redis;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.EnvironmentName);
builder.Services.AddScoped<DbSeeder>();

// Register caching services (cache-aside pattern for read-heavy, non-sensitive data)
// ⚠️ Security: Do NOT cache users (contains PII: email, phone, address)
// ⚠️ Avoid caching: auth tokens, passwords, sensitive personal data
builder.Services.AddScoped<ProductCacheService>();

// Configure JSON serialization with source generators for improved performance and AOT support
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolver = ApexShopJsonContext.Default;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// CORS Configuration (for browser-based clients and cross-origin requests)
// This is important for public e-commerce APIs accessed from web frontends
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()           // Allow requests from any origin
            .AllowAnyMethod()           // Allow any HTTP method
            .AllowAnyHeader();          // Allow any headers
    });

    // Production-ready policy (example - customize for your actual domains)
    options.AddPolicy("Production", policy =>
    {
        policy
            .WithOrigins(
                "https://example.com",
                "https://www.example.com",
                "https://admin.example.com")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

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

// Distributed caching with HybridCache (L1: Local memory, L2: Redis)
// Two-tier caching for read-heavy operations (products, users, categories)
// Configuration based on payload size analysis:
// - MaximumPayloadBytes: 1MB (supports product catalog entries up to ~500KB)
// - MaximumKeyLength: 512 chars (sufficient for nested cache keys)
// - Distributed TTL: 5 minutes (balance freshness vs database load)
// - Local TTL: 2 minutes (save memory while maintaining L1 benefits)
builder.Services.AddHybridCache(options =>
{
    options.MaximumPayloadBytes = 1024 * 1024; // 1 MB
    options.MaximumKeyLength = 512;

    // Default expiration times
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        // Distributed cache (Redis) expiration
        Expiration = TimeSpan.FromMinutes(5),
        // Local cache (in-memory) expiration (shorter to manage memory)
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    };
});

// Configure Redis distributed cache as L2 backing store for HybridCache
// Redis provides consistency across multiple API instances
// Graceful degradation: if Redis unavailable, falls back to local L1 cache only
var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? "localhost:6379"; // Development default

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnection;

    // Instance prefix prevents key collisions in shared Redis instances
    // Format: "ApexShop:{Environment}:" ensures isolation between environments
    var environment = builder.Environment.EnvironmentName;
    options.InstanceName = $"ApexShop:{environment}:";

    // Connection configuration with retry policy
    options.ConfigurationOptions = new StackExchange.Redis.ConfigurationOptions
    {
        EndPoints = { redisConnection },
        AbortOnConnectFail = false, // Don't fail startup if Redis unavailable
        ConnectTimeout = 5000,      // 5 second timeout
        SyncTimeout = 5000,
        AllowAdmin = false
    };
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

// ============================================================================
// MIDDLEWARE PIPELINE ORDER (Critical for Performance & Security)
// ============================================================================
// The order matters! Middleware executes in registration order.
// Each middleware can short-circuit the pipeline early.
//
// Execution Flow:
// 1. Exception handling catches errors from all downstream middleware
// 2. HTTPS/Security applied early for protection
// 3. Static files short-circuit early if matched
// 4. Routing determines which endpoint handles the request
// 5. CORS applied after routing (needs route info) but before auth
// 6. Authentication & Authorization (if needed)
// 7. Rate limiting (if needed)
// 8. Response compression (before cache)
// 9. Output cache
// 10. Health checks (short-circuit early - avoid processing by other middleware)
// 11. HTTP/3 headers (applies to all non-short-circuited responses)
// 12. OpenAPI/Swagger
// 13. Endpoints (terminal middleware)
// ============================================================================

// 1. EXCEPTION HANDLING (First - catches all downstream exceptions)
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
}

// 2. HTTPS & SECURITY (Early to protect all downstream traffic)
if (!app.Environment.IsDevelopment())
{
    // HSTS only in production - tells browsers to always use HTTPS
    app.UseHsts();
}
app.UseHttpsRedirection();

// 3. STATIC FILES (if needed)
// Uncomment if serving static content (CSS, JS, images, etc.)
// Short-circuits early if file is matched
// app.UseStaticFiles();

// 4. ROUTING (Required before auth/cors/authorization)
app.UseRouting();

// 5. CORS (After routing, before authentication)
// Select appropriate policy based on environment
var corsPolicy = app.Environment.IsDevelopment() ? "AllowAll" : "Production";
app.UseCors(corsPolicy);

// 6. AUTHENTICATION & AUTHORIZATION (if needed)
// Uncomment if your API requires authentication
// app.UseAuthentication();
// app.UseAuthorization();

// 7. RATE LIMITING (optional - after auth, before endpoints)
// Uncomment if you want to protect against abuse
// app.UseRateLimiter();

// 8. RESPONSE COMPRESSION (Before output cache for optimal stacking)
// Automatically negotiates Brotli or Gzip based on Accept-Encoding header
// Reduces payload sizes by 60-80% for JSON, 40-70% for streaming responses
app.UseResponseCompression();

// 9. OUTPUT CACHING (Caches GET responses for paginated and single-item endpoints)
app.UseOutputCache();

// 10. HEALTH CHECKS (Short-circuit to bypass other middleware)
// Performance monitoring endpoints that exit early without further processing
// Placed before HTTP/3 header and other endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true
}).ShortCircuit();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).ShortCircuit();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
}).ShortCircuit();

// 11. HTTP/3 PROTOCOL NEGOTIATION (After short-circuit opportunities)
// Advertises HTTP/3 capability to clients via Alt-Svc header
// Placed after health checks so it applies to all remaining responses
// This runs for all non-short-circuited requests
app.Use(async (context, next) =>
{
    context.Response.Headers.AltSvc = "h3=\":443\"; ma=86400";
    await next();
});

// 12. OPENAPI/SWAGGER (Development only)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 13. APPLICATION ENDPOINTS (Terminal middleware - executes last)
app.MapProductEndpoints();
app.MapCategoryEndpoints();
app.MapUserEndpoints();
app.MapOrderEndpoints();
app.MapReviewEndpoints();

// ============================================================================
// STARTUP LOGIC (Not middleware, but database initialization)
// ============================================================================

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

app.Run();

// Make Program accessible for WebApplicationFactory in tests/benchmarks
namespace ApexShop.API
{
    public partial class Program { }
}
