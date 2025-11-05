using ApexShop.API.Caching;
using ApexShop.API.Endpoints.Categories;
using ApexShop.API.Endpoints.Orders;
using ApexShop.API.Endpoints.Products;
using ApexShop.API.Endpoints.Reviews;
using ApexShop.API.Endpoints.Users;
using ApexShop.API.JsonContext;
using ApexShop.Infrastructure;
using ApexShop.Infrastructure.Data;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
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

    // Production-ready policy (uses environment configuration)
    options.AddPolicy("Production", policy =>
    {
        // Load allowed origins from appsettings.json
        // Example in appsettings.Production.json:
        // "Cors": {
        //   "AllowedOrigins": ["https://example.com", "https://www.example.com"]
        // }
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? new[] { "https://example.com" };  // Safe default

        policy
            .WithOrigins(allowedOrigins)
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

    // Connection configuration with retry policy for resilience
    options.ConfigurationOptions = new StackExchange.Redis.ConfigurationOptions
    {
        EndPoints = { redisConnection },
        AbortOnConnectFail = false,           // Don't fail startup if Redis unavailable
        ConnectTimeout = 5000,                // 5 second timeout
        SyncTimeout = 5000,
        ConnectRetry = 3,                     // Retry 3 times on connection failure
        AllowAdmin = false
    };
});

// Request decompression (must be registered BEFORE app.Build())
// Handles compressed POST/PUT bodies (Content-Encoding: gzip, deflate, br)
builder.Services.AddRequestDecompression();

// Configure Kestrel for HTTP/3 support (MUST be before app.Build())
// In Development: Kestrel reads from launchSettings.json
// In Production: Configure via appsettings.json or environment variables
//
// Example appsettings.json configuration:
// {
//   "Kestrel": {
//     "Endpoints": {
//       "Https": {
//         "Url": "https://*:443",
//         "Protocols": "Http1AndHttp2AndHttp3"
//       }
//     }
//   }
// }
//
// If you need programmatic Kestrel configuration (production only):
if (!builder.Environment.IsDevelopment())
{
    // Uncomment if needed, but appsettings.json is preferred
    // builder.WebHost.ConfigureKestrel(options =>
    // {
    //     options.ListenAnyIP(443, listenOptions =>
    //     {
    //         listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
    //         listenOptions.UseHttps();
    //     });
    // });
}

var app = builder.Build();

// ============================================================================
// MIDDLEWARE PIPELINE ORDER (Critical for Performance & Security)
// ============================================================================
// The order matters! Middleware executes in registration order.
// Each middleware can short-circuit the pipeline early.
//
// Execution Flow (Service Registration → Middleware Pipeline):
// 1. Exception handling catches errors from all downstream middleware
// 2. HTTPS/Security + HSTS (production only)
// 3. Security headers (X-Content-Type-Options, X-Frame-Options, etc.)
// 4. Static files short-circuit early if matched (if enabled)
// 5. Routing determines which endpoint handles the request
// 6. Request decompression (handles compressed POST/PUT bodies - Content-Encoding)
// 7. CORS applied after routing (needs route info) but before auth
// 8. Authentication & Authorization (if needed - currently disabled)
// 9. Rate limiting (if needed - currently disabled)
// 10. Response compression (before cache for optimal stacking)
// 11. Output cache (caches GET responses)
// 12. Health checks (short-circuit early - avoid processing by other middleware)
// 13. HTTP/3 headers (production only, after short-circuits)
// 14. Error handler endpoint (/error - catches exceptions from UseExceptionHandler)
// 15. OpenAPI/Swagger (dev only)
// 16. Endpoints (terminal middleware - actual API endpoints)
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
    // HTTPS redirect only in production to simplify local development
    app.UseHttpsRedirection();
}

// 3. SECURITY HEADERS (Protect against common attacks)
app.Use(async (context, next) =>
{
    // Prevent MIME-sniffing attacks
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

    // Prevent clickjacking attacks
    context.Response.Headers.Append("X-Frame-Options", "DENY");

    // Enable XSS protection in older browsers
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

    // Control referrer information
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

    // Content Security Policy (optional - uncomment if serving HTML)
    // context.Response.Headers.Append("Content-Security-Policy",
    //     "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:;");

    // Remove server header for security (don't reveal what we're running)
    context.Response.Headers.Remove("Server");

    await next();
});

// 4. STATIC FILES (if needed)
// Uncomment if serving static content (CSS, JS, images, etc.)
// Short-circuits early if file is matched
// app.UseStaticFiles();

// 5. ROUTING (Required before auth/cors/authorization)
app.UseRouting();

// 6. REQUEST DECOMPRESSION (Handle compressed POST/PUT bodies)
// Middleware to decompress request bodies (Content-Encoding: gzip, deflate, br)
app.UseRequestDecompression();

// 7. CORS (After routing, before authentication)
// Select appropriate policy based on environment
// Allow override via environment variable for testing
var corsPolicy = builder.Configuration.GetValue<string>("Cors:Policy")
    ?? (app.Environment.IsDevelopment() ? "AllowAll" : "Production");
app.UseCors(corsPolicy);

// 8. AUTHENTICATION & AUTHORIZATION (if needed)
// Uncomment if your API requires authentication
// app.UseAuthentication();
// app.UseAuthorization();

// 9. RATE LIMITING (optional - after auth, before endpoints)
// Uncomment if you want to protect against abuse
// app.UseRateLimiter();

// 10. RESPONSE COMPRESSION (Before output cache for optimal stacking)
// Automatically negotiates Brotli or Gzip based on Accept-Encoding header
// Reduces payload sizes by 60-80% for JSON, 40-70% for streaming responses
app.UseResponseCompression();

// 11. OUTPUT CACHING (Caches GET responses for paginated and single-item endpoints)
app.UseOutputCache();

// 12. HEALTH CHECKS (Short-circuit to bypass other middleware)
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

// 13. HTTP/3 PROTOCOL NEGOTIATION (Production only, after short-circuit opportunities)
// Advertises HTTP/3 capability to clients via Alt-Svc header
// Placed after health checks and only in production to reduce dev overhead
if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        // Dynamically determine the port (avoid hardcoding 443)
        var host = context.Request.Host;
        var port = host.Port ?? 443;

        // Alt-Svc header format: h3=":[port]"; ma=[max-age]
        context.Response.Headers.AltSvc = $"h3=\":{port}\"; ma=86400";
        await next();
    });
}

// 14. ERROR HANDLER ENDPOINT
// Handles exceptions from the exception handler middleware
app.Map("/error", (HttpContext context) =>
{
    var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
    var exception = exceptionHandlerFeature?.Error;

    // Always log the full exception for debugging
    if (exception != null)
    {
        context.RequestServices.GetService<ILogger<Program>>()
            ?.LogError(exception, "Unhandled exception occurred");
    }

    // Get environment to determine if we should expose details
    var environment = context.RequestServices
        .GetRequiredService<IWebHostEnvironment>();

    // In production, hide error details to prevent information leakage
    // (database connection strings, file paths, internal architecture, etc.)
    var errorDetail = environment.IsDevelopment()
        ? exception?.Message
        : "An unexpected error occurred. Please try again later.";

    return Results.Problem(
        title: "An error occurred",
        detail: errorDetail,
        statusCode: StatusCodes.Status500InternalServerError
    );
});

// 15. OPENAPI/SWAGGER (Development only)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 16. APPLICATION ENDPOINTS (Terminal middleware - executes last)
app.MapProductEndpoints();
app.MapCategoryEndpoints();
app.MapUserEndpoints();
app.MapOrderEndpoints();
app.MapReviewEndpoints();

// ============================================================================
// STARTUP LOGIC (Not middleware, but database initialization)
// ============================================================================

// Request size limits (optional - uncomment if needed)
// Prevents abuse from oversized payloads
// builder.Services.Configure<FormOptions>(options =>
// {
//     options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB
// });
// builder.Services.Configure<KestrelServerOptions>(options =>
// {
//     options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
// });

// Redis connection health check (Production only - optional)
// Verify Redis is available on startup
if (!app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        await cache.SetStringAsync("startup-health-check", "ok",
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5)
            });
        app.Logger.LogInformation("Redis connection verified - distributed cache operational");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Redis unavailable - HybridCache will use local L1 memory only");
        // Application continues - L1 local cache will still work for performance
    }
}

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
