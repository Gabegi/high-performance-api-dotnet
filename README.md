# high-performance-api-dotnet

Optimising Performance to the max for a .NET API

## Overview

A high-performance e-commerce API built with .NET 9 and PostgreSQL, designed to demonstrate production-grade performance optimization techniques. This project serves as a reference implementation for building scalable, low-latency APIs.

## Running the Benchmarks

### Quick Start (Interactive)

**Micro-Benchmarks:**
```bash
# Open PowerShell as Administrator (required for hardware counters)
dotnet run -c Release --project ApexShop.Benchmarks.Micro
```

**Load Tests:**

The load test suite automatically reseeds the database before running to ensure clean, consistent test data with no ID gaps.

```bash
# Terminal 1: Start load tests (will drop/recreate database)
cd ApexShop.LoadTests
dotnet run -c Release

# When prompted "Press any key to begin...", switch to Terminal 2
```

```bash
# Terminal 2: Start API with seeding enabled
cd ApexShop.API
$env:RUN_SEEDING="true"
dotnet run -c Release

# Wait for: "✓ Benchmark database seeded successfully!"
# Then return to Terminal 1 and press any key
```

**What the load test does automatically:**
1. 🗑️ Drops existing database (clears ID gaps from previous tests)
2. 🔨 Recreates database with migrations
3. ⏸️ Waits for you to start API with seeding
4. ✅ Runs all 12 test scenarios sequentially
5. 📊 Generates reports in `ApexShop.LoadTests/Reports/`

**Why reseeding is important:**
- Ensures continuous IDs (1, 2, 3... instead of 1, 5, 7, 12...)
- Eliminates 404 errors from random ID selection
- Provides consistent baseline for performance comparisons
- Achieves 95-100% success rates instead of false failures

### Automated Suite (Perfect for Overnight Runs)

Run both benchmarks and load tests sequentially with automatic shutdown:

```powershell
# Navigate to project directory
cd C:\Users\lelyg\Desktop\code\high-performance-api-dotnet

# Run tests normally (no shutdown)
.\run-benchmarks.ps1

# Run tests + shutdown computer (great for overnight benchmarking)
.\run-benchmarks.ps1 -Shutdown
```

**What it does:**
1. Runs BenchmarkDotNet micro-benchmarks
2. Runs NBomber load tests (auto-selects baseline)
3. Shuts down computer after 5-minute countdown (if `-Shutdown` used)

**Perfect for:**
- 🌙 Overnight benchmark runs
- 💻 Leaving long tests running unattended
- 🔋 Saving power after tests complete

**Results saved to:**
- Benchmarks: `ApexShop.Benchmarks.Micro/Reports/`
- Load Tests: `ApexShop.LoadTests/Reports/`

**Troubleshooting:**
If you get an execution policy error:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```
### Key Features

- **High Throughput**: Optimized for 10,000+ requests per second
- **Low Latency**: Sub-50ms p99 response times
- **Production-Ready**: Realistic e-commerce schema with 47,500+ seeded records
- **Comprehensive Testing**: Micro benchmarks and load tests included
- **Performance Monitoring**: Built-in diagnostics and profiling

### Technologies

- **.NET 9**: Latest runtime with performance improvements
- **PostgreSQL 16**: High-performance relational database
- **EF Core 9**: Optimized ORM with advanced query capabilities
- **Minimal APIs**: Low-overhead endpoint routing
- **BenchmarkDotNet**: Micro-benchmark framework
- **NBomber**: Load testing framework
- **Bogus**: Realistic data generation
- **MessagePack**: Binary serialization format (60% size reduction)

### Advanced Performance Features

#### 1. Content Negotiation with Multiple Serialization Formats

The API supports dynamic content negotiation across all streaming endpoints, allowing clients to request their preferred serialization format via the `Accept` header:

**Supported Formats:**
- **MessagePack** (`application/x-msgpack`): Binary format with ~60% size reduction and 5-10x faster serialization
- **NDJSON** (`application/x-ndjson`): Newline-delimited JSON for line-by-line parsing without buffering entire response
- **JSON** (`application/json`): Standard JSON (default fallback)

**Streaming Endpoints with Content Negotiation:**
- `GET /products/stream`
- `GET /categories/stream`
- `GET /orders/stream`
- `GET /reviews/stream`
- `GET /users/stream`

**Example Usage:**
```bash
# Request MessagePack format (binary, most compact)
curl -H "Accept: application/x-msgpack" https://api.example.com/products/stream

# Request NDJSON format (streaming-friendly)
curl -H "Accept: application/x-ndjson" https://api.example.com/products/stream

# Default JSON format
curl https://api.example.com/products/stream
```

**Benefits:**
- **MessagePack**: Ideal for bandwidth-constrained clients and high-throughput scenarios
- **NDJSON**: Perfect for streaming parsers and downstream processing pipelines
- **JSON**: Standard format for web browsers and REST clients

#### 2. HTTP/3 Support (QUIC Protocol)

The API fully supports HTTP/3, the latest HTTP protocol offering:

**Configuration:**
- Enabled via Kestrel with `HttpProtocols.Http1AndHttp2AndHttp3`
- Alt-Svc header advertised automatically for protocol upgrade
- Backward compatible with HTTP/1.1 and HTTP/2

**Benefits:**
- **Reduced Latency**: UDP-based QUIC eliminates TCP handshake overhead
- **Multiplexing**: Better handling of multiple concurrent streams
- **Connection Migration**: Seamless switching between networks (WiFi → mobile)
- **0-RTT**: Faster connection establishment for repeat clients

**To use HTTP/3:**
```bash
# curl automatically upgrades to HTTP/3 if available
curl --http3 https://api.example.com/products

# Verify protocol negotiation
curl -I --http3 https://api.example.com/products
```

#### 3. Smart Output Caching with Tag-Based Invalidation

Production-grade caching system for paginated and single-item endpoints with atomic tag-based invalidation:

**Caching Policies:**
- **"Lists" Policy**: 10-minute TTL for paginated endpoints (`GET /resource?page=X&pageSize=Y`)
- **"Single" Policy**: 15-minute TTL for single-item endpoints (`GET /resource/{id}`)
- **No Caching**: Streaming endpoints intentionally excluded (already memory-efficient)

**Cached Endpoints:**
- `GET /products` (paginated)
- `GET /products/cursor` (keyset pagination)
- `GET /products/{id}` (single item)
- Similar patterns for `/categories`, `/orders`, `/reviews`, `/users`

**Cache Invalidation Strategy:**
- **Smart Invalidation**: Write operations atomically clear only relevant cache tags
- **Tag-Based**: All related caches cleared with single operation
- **Atomic**: No race conditions between cache clear and new writes

**Cache Behavior Examples:**
```
POST /products → Invalidates "lists" tag
PUT /products/{id} → Invalidates "lists" + "single" tags
DELETE /products/{id} → Invalidates "lists" + "single" tags
GET /products/stream → NOT cached (streaming is memory-efficient)
GET /products/export/ndjson → NOT cached (always fresh export)
```

**Performance Impact:**
- Eliminates repeated database queries for list views
- Reduces serialization overhead for frequently-accessed single items
- Tag-based invalidation ensures consistency without cache stampedes

#### 4. Automatic HTTP Response Compression (Brotli + Gzip)

Transparent response compression reduces payload sizes without requiring client changes:

**Compression Configuration:**
- **Primary**: Brotli (br) - ~15-20% better compression than Gzip
- **Fallback**: Gzip (gzip) - For older clients and broader compatibility
- **Level**: Fastest (optimizes for latency over compression ratio)
- **HTTPS**: Enabled (safe with modern TLS, no CRIME vulnerability risk)

**Compressed MIME Types:**

The API extends the ASP.NET Core defaults with custom MIME types for better API performance:

```csharp
options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
{
    "application/json",        // Custom API responses
    "application/x-ndjson",    // Streaming JSON
    "image/svg+xml"            // SVG is text-based XML
});
```

**Includes all defaults plus:**
- `application/json` - Custom JSON API responses
- `application/x-ndjson` - Newline-delimited JSON streams
- `image/svg+xml` - SVG graphics (text-based, compresses well)

**Default types covered:**
- **Text**: `text/html`, `text/css`, `text/plain`, `text/xml`, `text/javascript`
- **Application**: `application/xml`, `application/javascript`
- **And more**: Maintained by ASP.NET Core defaults

**How It Works:**
```
Client Request:
GET /products HTTP/1.1
Accept-Encoding: br, gzip

Server Response:
HTTP/1.1 200 OK
Content-Encoding: br
Content-Length: 85234  (compressed)
[compressed payload]

Client Browser/SDK:
Automatically decompresses using declared encoding
```

**Payload Size Reductions:**

| Scenario | Uncompressed | Compressed | Reduction |
|----------|-------------|-----------|-----------|
| 15K products list (JSON) | ~500KB | ~80-100KB | 80-85% |
| Single item (JSON) | ~5KB | ~1.5KB | 70% |
| Streaming 5K items (NDJSON) | ~300KB | ~50-100KB | 67-83% |
| MessagePack binary + compression | ~60KB | ~15-30KB | 50-75% |

**Performance Benefits:**
- **Network**: Reduced bandwidth usage (critical for mobile/poor connections)
- **Latency**: Faster transfer times on high-latency networks (4G, satellite, etc.)
- **Cost**: Lower data transfer costs for cloud-hosted APIs
- **Automatic**: No client-side configuration needed (HTTP standard)
- **Stacking**: Works seamlessly with content negotiation and output caching

**Combined Optimization Stack:**

When a client requests compressed NDJSON from a paginated endpoint:
```bash
curl -H "Accept: application/x-ndjson" \
     -H "Accept-Encoding: br" \
     https://api.example.com/products?page=1&pageSize=100
```

The response pipeline:
1. **Output Cache Hit** → Serves cached response (10 min TTL)
2. **Content Negotiation** → Routes to NDJSON formatter
3. **Response Compression** → Applies Brotli compression
4. **Result**: ~80-90% size reduction vs raw JSON

**Testing Compression:**
```bash
# Verify compression is working
curl -I -H "Accept-Encoding: br, gzip" https://api.example.com/products
# Look for: Content-Encoding: br (or gzip)

# Compare compressed vs uncompressed sizes
uncompressed=$(curl -s https://api.example.com/products | wc -c)
compressed=$(curl -s -H "Accept-Encoding: br" https://api.example.com/products | wc -c)
echo "Uncompressed: $uncompressed bytes"
echo "Compressed: $compressed bytes"
echo "Ratio: $(echo "scale=2; $compressed/$uncompressed*100" | bc)%"
```

#### 5. Two-Tier HybridCache (L1 Local + L2 Redis)

Advanced distributed caching for read-heavy, non-sensitive data with automatic failover and tag-based invalidation:

**Architecture:**
```
┌─────────────────────────────────────────────────────────────┐
│                    Request for Data                          │
└────────────────────────┬────────────────────────────────────┘
                         ↓
            ┌────────────────────────┐
            │  L1: Local Memory      │ ✓ HIT: ~1-10µs
            │  (2-min TTL)           │ ✗ MISS: Continue
            │  Per-instance          │
            └────────────┬───────────┘
                         ↓
            ┌────────────────────────┐
            │  L2: Redis (Distributed)│ ✓ HIT: ~1-5ms
            │  (5-min TTL)           │ ✗ MISS: Continue
            │  Shared across instances│
            └────────────┬───────────┘
                         ↓
            ┌────────────────────────┐
            │  L3: Database          │ ✓ HIT: ~5-50ms
            │  (Source of truth)     │ (Factory function)
            └────────────┬───────────┘
                         ↓
                   Return Data
```

**Configuration:**
- **L1 TTL**: 2 minutes (saves memory, fast hits)
- **L2 TTL**: 5 minutes (distributed consistency)
- **Count Cache**: 15 minutes (expensive query, changes rarely)
- **Max Payload**: 1MB per entry (supports ~500KB product data)
- **Max Key Length**: 512 characters

**What to Cache (✅ Safe)**
- **Products**: Read-heavy, infrequent changes, no PII
- **Categories**: Static reference data, optimal cache candidate
- **Orders**: Completed orders, non-sensitive transaction data
- **Reviews**: Public user feedback, anonymizable

**What NOT to Cache (❌ Security Risk)**
- **Users**: Contains PII (email, phone, address, billing info)
- **Auth tokens**: Security risk if cached/compromised
- **Passwords**: Never, under any circumstance
- **Shopping carts**: User-specific, frequently changing
- **Sensitive configurations**: API keys, database passwords

**Tag-Based Invalidation (Redis Feature)**

Atomic bulk removal without manual loops:

```csharp
// ✅ EFFICIENT: One call removes ALL tagged entries
await cache.RemoveByTagAsync("products");  // Removes all product caches

// ❌ INEFFICIENT (OLD): Manual loop, 10-100+ calls
for (int page = 1; page <= 100; page++)
{
    await cache.RemoveAsync($"products:page:{page}");  // Wasteful!
}
```

**Example: Product Update Flow**

```csharp
public async Task UpdateProductAsync(int id, UpdateProductRequest req)
{
    // 1. Update database
    var product = await _repository.UpdateAsync(id, req);

    // 2. Invalidate specific product cache
    await _cache.RemoveAsync(CacheKeys.Product.ById(id));

    // 3. Invalidate all product-related caches (ONE call via tag)
    await _cache.RemoveByTagAsync(CacheKeys.Product.Tag);

    // 4. Also invalidate category cache if category changed
    if (product.CategoryId != req.CategoryId)
    {
        await _cache.RemoveByTagAsync(
            CacheKeys.Product.CategoryTag(product.CategoryId));
    }

    return product;
}
```

**Performance Benefits**
- **L1 hits**: Microsecond response times (same server memory)
- **L2 hits**: Millisecond response times (across network)
- **DB fallback**: Automatic if L1+L2 both miss
- **Graceful degradation**: Works if Redis unavailable (falls back to L1 only)
- **Distributed consistency**: Changes synchronized across instances

**Monitoring & Debugging**

```bash
# Connect to Redis and inspect cache
redis-cli
> KEYS "ApexShop:Production:*"
> TTL "ApexShop:Production:Product:123"
> GET "ApexShop:Production:Product:123"
> FLUSHDB  # Clear all caches (development only!)
```

**Configuration (appsettings.json)**

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"  // Development
    // Production: "redis-prod.example.com:6379" with auth
  }
}
```

#### 6. Standardized Pagination System with API Versioning

Consistent, reusable pagination across all list endpoints with v2 endpoints providing an improved response format:

**V1 Endpoints (Existing - Backward Compatible):**
```
GET /products?page=1&pageSize=50
GET /users?page=1&pageSize=50
GET /reviews?page=1&pageSize=50
GET /orders?page=1&pageSize=50
GET /categories?page=1&pageSize=50
```

**V2 Endpoints (Recommended - Enhanced Features):**
```
GET /products/v2?page=1&pageSize=50
GET /users/v2?page=1&pageSize=50
GET /reviews/v2?page=1&pageSize=50
GET /orders/v2?page=1&pageSize=50
GET /categories/v2?page=1&pageSize=50
```

**V2 Response Format:**
```json
{
  "data": [
    { "id": 1, "name": "Product A", ... },
    { "id": 2, "name": "Product B", ... }
  ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 15000,
  "totalPages": 300,
  "hasPrevious": false,
  "hasNext": true
}
```

**Key Features:**
- **Immutable Response**: PagedResult<T> properties are read-only after construction (safety)
- **Null-Safe**: Handles null data gracefully, preventing serialization errors
- **Max Page Size**: Automatically enforces 100-item maximum (configurable via PaginationParams.MaxPageSize)
- **Reusable Logic**: ToPagedListAsync extension method eliminates pagination code duplication
- **Stable Sorting**: All endpoints use OrderBy/OrderByDescending for consistent pagination

**Query Parameters:**
| Parameter | Type | Default | Max | Description |
|-----------|------|---------|-----|-------------|
| page | int | 1 | ∞ | 1-based page number |
| pageSize | int | 20 | 100 | Items per page (auto-clamped to max) |

**Migration Path (6-Month Deprecation):**

1. **Phase 1 (Immediate)**: V2 endpoints available alongside V1
2. **Phase 2 (Month 1-5)**: Encourage clients to migrate to /v2 endpoints
3. **Phase 3 (Month 6)**: V1 endpoints marked as deprecated in documentation
4. **Phase 4 (Month 6+)**: Monitor V1 usage; consider removal if <5% traffic

**Implementation Details:**

```csharp
// PaginationParams - Request model
public class PaginationParams
{
    private const int MaxPageSize = 100;
    private int _pageSize = 20;

    public int Page { get; set; } = 1;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
    }
}

// PagedResult<T> - Response wrapper
public class PagedResult<T>
{
    public IReadOnlyList<T> Data { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

// Usage in endpoint
var result = await query
    .OrderBy(p => p.Id)  // ← REQUIRED before pagination
    .Select(p => new ProductListDto(...))
    .ToPagedListAsync(pagination.Page, pagination.PageSize, cancellationToken);
return Results.Ok(result);
```

**Performance Characteristics:**
- **COUNT Query**: Runs once per request to get total count (optimization: consider caching for expensive queries)
- **Skip/Take Query**: Efficiently translates to SQL OFFSET/LIMIT
- **Memory**: Uses IReadOnlyList to prevent accidental mutation

**Example Requests:**

```bash
# First page (20 items default)
curl https://api.example.com/products/v2

# Custom page size
curl https://api.example.com/products/v2?page=1&pageSize=50

# Last page detection
curl https://api.example.com/products/v2?page=300

# Response shows if more pages exist
# "hasNext": true  → can request page=2
# "hasNext": false → already on last page
```

**Backward Compatibility:**
- V1 endpoints (without /v2) continue to work unchanged
- Both versions available indefinitely during transition period
- No forced migration required for existing clients

#### 7. Optimized Middleware Pipeline Order

The API uses a highly optimized middleware pipeline order that's critical for both security and performance:

**Middleware Execution Order:**

```
1.  Exception Handling           → Catches all downstream exceptions
2.  HTTPS & HSTS                → HTTPS redirect + security headers (prod only)
3.  Static Files                → Short-circuit for static content (optional)
4.  Routing                      → Determines which endpoint handles request
5.  CORS                         → Cross-Origin Resource Sharing (after routing)
6.  Authentication               → Identifies the user (optional)
7.  Authorization                → Checks user permissions (optional)
8.  Rate Limiting                → Protects against abuse (optional)
9.  Response Compression         → Brotli/Gzip (before cache)
10. Output Cache                 → Caches GET responses (10-15 min TTL)
11. Health Checks                → Short-circuit (skip other middleware)
12. HTTP/3 Headers               → Alt-Svc protocol negotiation
13. OpenAPI/Swagger              → Development only
14. Endpoints                    → Terminal middleware (handles requests)
```

**Why This Order Matters:**

1. **Exception handling first** → Catches errors from all downstream middleware
2. **HTTPS early** → Protects all traffic before processing
3. **Routing before CORS** → CORS middleware needs routing info
4. **CORS before auth** → Auth middleware needs CORS context
5. **Compression before cache** → Cache stores already-compressed responses (stacking optimization)
6. **Health checks short-circuit** → Exit early without processing other middleware (performance)
7. **HTTP/3 headers after short-circuits** → Applies to all responses except short-circuited ones
8. **Endpoints last** → Terminal middleware handles actual requests

**CORS Configuration:**

The API supports environment-aware CORS policies:

```csharp
// Development: Permissive policy (AllowAll)
// Allows requests from any origin for easy local testing

// Production: Restricted policy
// Only allows specific trusted origins:
// - https://example.com
// - https://www.example.com
// - https://admin.example.com

// Usage:
var corsPolicy = app.Environment.IsDevelopment() ? "AllowAll" : "Production";
app.UseCors(corsPolicy);
```

**Environment-Aware Exception Handling:**

```csharp
if (app.Environment.IsDevelopment())
{
    // Developer Exception Page - detailed error info (dev only)
    app.UseDeveloperExceptionPage();
}
else
{
    // Custom error handler - safe for production
    app.UseExceptionHandler("/error");
}
```

**Optional Features (Disabled by Default):**

The following middleware is documented but commented out by default. Uncomment as needed:

```csharp
// Authentication (uncomment if your API requires login)
// app.UseAuthentication();

// Authorization (uncomment if your API requires permission checks)
// app.UseAuthorization();

// Rate Limiting (uncomment to protect against abuse)
// app.UseRateLimiter();

// Static Files (uncomment if serving CSS, JS, images)
// app.UseStaticFiles();
```

**Performance Benefits:**

- **Early short-circuiting** → Health checks exit immediately (no auth/cache/compression overhead)
- **Optimal compression stacking** → Cache stores pre-compressed responses
- **Reduced middleware overhead** → Only essential middleware runs for each request
- **Security-first design** → HTTPS protection before any request processing

**Example Request Flow (Health Check):**

```
Request: GET /health
→ Exception Handling (skip)
→ HTTPS/Security (skip)
→ Routing (matches /health endpoint)
→ CORS (skip)
→ Auth (skip)
→ Compression (skip)
→ Cache (skip)
→ Health Check Match → ShortCircuit()
✓ Response returned (status 200)
(HTTP/3 header, Endpoints middleware SKIPPED due to short-circuit)
```

**Example Request Flow (API Endpoint):**

```
Request: GET /products/v2?page=1&pageSize=50
→ Exception Handling (catches errors)
→ HTTPS/Security (redirects if needed)
→ Routing (matches /products/v2)
→ CORS (apply policy)
→ Authentication (if enabled)
→ Authorization (if enabled)
→ Compression Middleware (enable compression)
→ Output Cache (check cache)
  ✓ Cache HIT → Return cached compressed response
  ✗ Cache MISS → Continue
→ HTTP/3 Header (add Alt-Svc)
→ Endpoint (execute endpoint handler)
→ Cache stored response
→ Response returned with compression
```

### Who Is This For?

- Developers building high-performance APIs
- Teams optimizing existing .NET applications
- Anyone learning performance engineering in .NET

## API Endpoint Reference

The API provides 67 endpoints across 5 resource types (Products, Orders, Categories, Reviews, Users). Each resource supports multiple access patterns optimized for different use cases.

### Endpoint Summary by Resource

**ℹ️ Benchmark Coverage Note:** All entities (Products, Orders, Categories, Reviews, Users) implement identical endpoint patterns and operations. The Products resource is comprehensively benchmarked to cover all architectural patterns (single operations, bulk operations, offset/cursor pagination, streaming, NDJSON export). Performance characteristics are uniform across resources, making Products the representative benchmark for the entire API.

#### **PRODUCTS** (`/products`) - 13 endpoints - ✅ FULLY BENCHMARKED

| HTTP | Endpoint | Format | Features | Rate Limit | Benchmarked |
|------|----------|--------|----------|-----------|-------------|
| GET | `/` | JSON | Offset pagination, cached (10m) | ❌ | ✅ |
| GET | `/v2` | PagedResult | Standardized pagination, cached (10m) | ❌ | ✅ |
| GET | `/cursor` | JSON | Cursor-based (O(1) perf), cached (10m) | ❌ | ✅ |
| GET | `/stream` | JSON Array | Content negotiation (JSON/NDJSON/MessagePack), unbuffered | ❌ | ✅ |
| GET | `/export/ndjson` | NDJSON | Streaming export, rate limited, max 100K records | ✅ 5/min | ✅ |
| GET | `/{id}` | JSON | Single item, cached (15m) | ❌ | ✅ |
| POST | `/` | JSON | Create single, clears "lists" cache | ❌ | ✅ |
| POST | `/bulk` | JSON | Batch create, clears "lists" cache | ❌ | ✅ |
| PUT | `/{id}` | JSON | Update single, clears both caches | ❌ | ✅ |
| PUT | `/bulk` | JSON | Batch update with streaming, clears both caches | ❌ | ✅ |
| DELETE | `/{id}` | JSON | Delete single, clears both caches | ❌ | ✅ |
| DELETE | `/bulk` | JSON | Batch delete (ExecuteDeleteAsync), clears both caches | ❌ | ✅ |
| PATCH | `/bulk-update-stock` | JSON | Update stock by category, direct SQL | ❌ | ✅ |

**Key Filters:** `?categoryId=1`, `?minPrice=100&maxPrice=500`, `?inStock=true`, `?modifiedAfter=2024-01-01`

#### **ORDERS** (`/orders`) - 10 endpoints

| HTTP | Endpoint | Format | Features | Rate Limit | Benchmarked |
|------|----------|--------|----------|-----------|-------------|
| GET | `/` | JSON | Offset pagination, cached (10m) | ❌ | ❌ |
| GET | `/v2` | PagedResult | Standardized pagination, cached (10m) | ❌ | ❌ |
| GET | `/cursor` | JSON | Cursor-based, cached (10m) | ❌ | ❌ |
| GET | `/stream` | JSON Array | Content negotiation, unbuffered | ❌ | ❌ |
| GET | `/export/ndjson` | NDJSON | Streaming export, rate limited, max 100K records | ✅ 5/min | ❌ |
| GET | `/{id}` | JSON | Single item, cached (15m) | ❌ | ❌ |
| POST | `/` | JSON | Create single, clears "lists" cache | ❌ | ❌ |
| PUT | `/{id}` | JSON | Update single, clears both caches | ❌ | ❌ |
| DELETE | `/{id}` | JSON | Delete single, clears both caches | ❌ | ❌ |
| DELETE | `/bulk-delete-old` | JSON | Delete old delivered orders | ❌ | ❌ |

**Key Filters:** `?customerId=5`, `?status=Shipped`, `?fromDate=2024-01-01&toDate=2024-12-31`, `?minAmount=1000`

#### **CATEGORIES** (`/categories`) - 11 endpoints

| HTTP | Endpoint | Format | Features | Rate Limit | Benchmarked |
|------|----------|--------|----------|-----------|-------------|
| GET | `/` | JSON | Offset pagination, cached (10m) | ❌ | ❌ |
| GET | `/v2` | PagedResult | Standardized pagination, cached (10m) | ❌ | ❌ |
| GET | `/stream` | JSON Array | Content negotiation, unbuffered | ❌ | ❌ |
| GET | `/export/ndjson` | NDJSON | Streaming export, rate limited, max 100K records | ✅ 5/min | ❌ |
| GET | `/{id}` | JSON | Single item, cached (15m) | ❌ | ❌ |
| POST | `/` | JSON | Create single, clears "lists" cache | ❌ | ❌ |
| POST | `/bulk` | JSON | Batch create, clears "lists" cache | ❌ | ❌ |
| PUT | `/{id}` | JSON | Update single, clears both caches | ❌ | ❌ |
| PUT | `/bulk` | JSON | Batch update, clears both caches | ❌ | ❌ |
| DELETE | `/{id}` | JSON | Delete single, clears both caches | ❌ | ❌ |
| DELETE | `/bulk` | JSON | Batch delete, clears both caches | ❌ | ❌ |

#### **REVIEWS** (`/reviews`) - 14 endpoints

| HTTP | Endpoint | Format | Features | Rate Limit | Benchmarked |
|------|----------|--------|----------|-----------|-------------|
| GET | `/` | JSON | Offset pagination, cached (10m) | ❌ | ❌ |
| GET | `/v2` | PagedResult | Standardized pagination, cached (10m) | ❌ | ❌ |
| GET | `/cursor` | JSON | Cursor-based, cached (10m) | ❌ | ❌ |
| GET | `/stream` | JSON Array | Content negotiation, unbuffered | ❌ | ❌ |
| GET | `/export/ndjson` | NDJSON | Streaming export, rate limited, max 100K records | ✅ 5/min | ❌ |
| GET | `/{id}` | JSON | Single item, cached (15m) | ❌ | ❌ |
| POST | `/` | JSON | Create single, clears "lists" cache | ❌ | ❌ |
| POST | `/bulk` | JSON | Batch create, clears "lists" cache | ❌ | ❌ |
| PUT | `/{id}` | JSON | Update single, clears both caches | ❌ | ❌ |
| PUT | `/bulk` | JSON | Batch update, clears both caches | ❌ | ❌ |
| DELETE | `/{id}` | JSON | Delete single, clears both caches | ❌ | ❌ |
| DELETE | `/bulk` | JSON | Batch delete, clears both caches | ❌ | ❌ |
| DELETE | `/product/{productId}/bulk-delete-old` | JSON | Delete old product reviews | ❌ | ❌ |

**Key Filters:** `?productId=10`, `?userId=5`, `?minRating=4`

#### **USERS** (`/users`) - 13 endpoints

| HTTP | Endpoint | Format | Features | Rate Limit | Benchmarked |
|------|----------|--------|----------|-----------|-------------|
| GET | `/` | JSON | Offset pagination, cached (10m) | ❌ | ❌ |
| GET | `/v2` | PagedResult | Standardized pagination, cached (10m) | ❌ | ❌ |
| GET | `/cursor` | JSON | Cursor-based, cached (10m) | ❌ | ❌ |
| GET | `/stream` | JSON Array | Content negotiation, unbuffered | ❌ | ❌ |
| GET | `/export/ndjson` | NDJSON | Streaming export, rate limited, max 100K records | ✅ 5/min | ❌ |
| GET | `/{id}` | JSON | Single item, cached (15m) | ❌ | ❌ |
| POST | `/` | JSON | Create single, clears "lists" cache | ❌ | ❌ |
| POST | `/bulk` | JSON | Batch create, clears "lists" cache | ❌ | ❌ |
| PUT | `/{id}` | JSON | Update single, clears both caches | ❌ | ❌ |
| PUT | `/bulk` | JSON | Batch update, clears both caches | ❌ | ❌ |
| DELETE | `/{id}` | JSON | Delete single, clears both caches | ❌ | ❌ |
| DELETE | `/bulk` | JSON | Batch delete, clears both caches | ❌ | ❌ |
| PATCH | `/bulk-deactivate-inactive` | JSON | Deactivate inactive users | ❌ | ❌ |

**Key Filters:** `?isActive=true`, `?createdAfter=2024-01-01`

### Export vs Stream Endpoints

**Streaming Endpoints** (`/stream`):
- Return JSON arrays (traditional format)
- Support content negotiation (JSON/NDJSON/MessagePack)
- No rate limiting
- Ideal for progressive parsing with content negotiation

**Export Endpoints** (`/export/ndjson`):
- Return NDJSON (newline-delimited JSON)
- **Rate limited**: 5 requests per minute per user
- **Max records**: 100,000 (configurable)
- **Advantages**: Error recovery, progressive parsing, no memory buffering
- Ideal for data pipelines and bulk operations

### Pagination Strategies

**Offset Pagination** (Traditional):
- Endpoint: `GET /{resource}?page=1&pageSize=50`
- Perfect for: UI pagination, small datasets
- Limitation: O(n) performance for deep pages

**Standardized Pagination** (v2):
- Endpoint: `GET /{resource}/v2?page=1&pageSize=50`
- Returns: `PagedResult<T>` with metadata (HasPrevious, HasNext, TotalPages)
- Perfect for: RESTful APIs, client-side logic

**Cursor-Based Pagination** (Keyset):
- Endpoint: `GET /{resource}/cursor?afterId=100&pageSize=50`
- Performance: O(1) for any page depth
- Perfect for: Infinite scroll, large datasets

### Rate Limiting

Export endpoints are rate-limited to **5 requests per minute per authenticated user**:
- Response: `HTTP 429 Too Many Requests`
- Headers include: `Retry-After` with seconds to wait

## Contributing

Contributions are welcome! If you have suggestions for improvements, please open an issue or submit a pull request.

### How to Contribute

1.  Fork the repository.
2.  Create a new branch for your feature or bug fix.
3.  Make your changes and commit them with a clear message.
4.  Push your changes to your fork.
5.  Open a pull request to the main repository.

## Architecture

This project follows a **Vertical Slice Architecture** with the primary objective of achieving **highest performance**.

## Project Structure

The project is organized into the following directories:

-   `ApexShop.API`: The main API project, containing the endpoints and `Program.cs`.
-   `ApexShop.Application`: Contains the application logic, such as services and DTOs.
-   `ApexShop.Domain`: Contains the domain entities and interfaces.
-   `ApexShop.Infrastructure`: Contains the infrastructure code, such as the `DbContext`, repositories, and database seeding.
-   `ApexShop.Benchmarks.Micro`: Contains the micro-benchmarks for the API.
-   `ApexShop.LoadTests`: Contains the load tests for the API.

## Infrastructure

### Database Selection: PostgreSQL vs SQL Server

**PostgreSQL** is the chosen database for this project for the following reasons:

- **Superior Performance**: PostgreSQL consistently outperforms SQL Server in high-throughput scenarios, especially for read-heavy workloads
- **Better Connection Pooling**: Native connection pooling with PgBouncer provides exceptional scalability
- **Efficient Indexes**: Advanced indexing capabilities (BRIN, GiST, GIN) and partial indexes offer better query optimization
- **Lower Latency**: Lightweight protocol and efficient buffer management result in lower query latency
- **Cost-Effective**: Open-source with no licensing costs, allowing infrastructure budget to focus on hardware optimization
- **JSON Performance**: Native JSONB support with indexing outperforms SQL Server's JSON handling
- **Concurrent Connections**: MVCC (Multi-Version Concurrency Control) handles concurrent writes more efficiently than SQL Server's locking mechanisms

### Database Schema

The application uses a production-realistic e-commerce database schema with the following entities:

#### Seed Data Statistics

| Entity | Row Count | Description |
|--------|-----------|-------------|
| **Categories** | 15 | Product categories (Electronics, Clothing, Books, etc.) |
| **Users** | 3,000 | Customer accounts with realistic contact details |
| **Products** | 15,000 | Products with realistic names/descriptions via Bogus |
| **Orders** | 5,000 | Customer orders with various statuses |
| **OrderItems** | ~12,500 | Order line items (1-5 items per order, avg 2.5) |
| **Reviews** | 12,000 | Product reviews (~80% products have reviews) |

**Total Rows:** ~47,500+
**Estimated Database Size:** 60-120 MB

#### Seeding Strategy

**Bogus-based seeding** for realistic data:
- **Smart seeding**: Automatically skips if data exists
- **Batched inserts**: 500-1000 records per batch for performance
- **ChangeTracker clearing**: Prevents memory issues with large datasets
- **Realistic data**: Uses Faker library for names, emails, addresses, product descriptions
- **Optimized queries**: Loads reference data upfront to avoid N+1 queries

#### Entity Relationships

- **User → Orders** (1:N): Each user can place multiple orders
- **User → Reviews** (1:N): Each user can write multiple reviews
- **Category → Products** (1:N): Each category contains multiple products
- **Product → OrderItems** (1:N): Each product can appear in multiple orders
- **Product → Reviews** (1:N): Each product can have multiple reviews
- **Order → OrderItems** (1:N): Each order contains multiple line items
- **OrderItem → Product** (N:1): Each order item references one product

#### Performance Optimizations

- **Indexes**: Strategic indexes on foreign keys, frequently queried columns (Price, Name, OrderDate, Status, Rating)
- **Precision**: Decimal fields use `PRECISION(18,2)` for monetary values
- **Constraints**: Appropriate delete behaviors (Cascade for dependent data, Restrict for referenced data)
- **Default Values**: Database-level defaults for timestamps using `CURRENT_TIMESTAMP`

## Database Setup with Docker

### Prerequisites

- Docker Desktop installed and running
- .NET 9 SDK

### Step 1: Create docker-compose.yml

Create a `docker-compose.yml` file in the project root with PostgreSQL configuration.

### Step 2: Configure Connection Strings

Add database connection settings to `appsettings.json` and `appsettings.Development.json`.

### Step 3: Configure DbContext

Set up EF Core with PostgreSQL provider in the Infrastructure layer, registering the DbContext in dependency injection.

### Step 4: Create Initial Migration

Generate EF Core migration for the domain entities:

```bash
dotnet ef migrations add InitialCreate --project ApexShop.Infrastructure --startup-project ApexShop.API
```

### Step 5: Start Database and Apply Migrations

```bash
# Start PostgreSQL container
docker-compose up -d

# Apply migrations
dotnet ef database update --project ApexShop.Infrastructure --startup-project ApexShop.API
```

### Step 6: Verify Connection

Run the API and verify database connectivity:

```bash
dotnet run --project ApexShop.API
```

## Baseline Results (Non-Optimized)

This section documents the **initial performance baseline** before any optimization work. These results establish the starting point for measuring optimization improvements.

### Micro-Benchmark Results (BenchmarkDotNet)
earliest report 2025-10-06_11-09-32 
current newest 2025-10-17_19-22-55   

**Test Environment:**
- CPU: Intel Core i7-8650U @ 1.90GHz (Kaby Lake R), 4 cores, 8 logical processors
- Runtime: .NET 9.0.9
- OS: Windows 11

| Benchmark | Mean | StdDev | Min | Max | Allocated Memory |
|-----------|------|--------|-----|-----|------------------|
| **Api_TrueColdStart** | 193.02 ms | 0.00 ms | 193.02 ms | 193.02 ms | 4,492 KB |
| **Api_GetSingleProduct** | 5.11 ms | 0.88 ms | 4.05 ms | 7.04 ms | 84 KB |
| **Api_ColdStart** | 180.23 ms | 13.00 ms | 157.43 ms | 202.04 ms | 4,406 KB |
| **Api_GetAllProducts** | 417.29 ms | 129.75 ms | 145.03 ms | 563.52 ms | 50,585 KB |

**Hardware Counters:**
- Cache misses for GetAllProducts: 12.2M per operation
- Branch instructions: 198M per operation
- Significant memory allocation on collection endpoints

### Load Testing Results (NBomber)

The load tests revealed **severe performance degradation** under realistic production load.

#### Overall Performance

- **Total Requests**: 12,117
- **Successful**: 144 (1.2%)
- **Failed**: 11,973 (98.8%)
- **Primary Failure Mode**: Operation timeouts (30s exceeded)

#### CRUD Scenarios (30s duration, 10 RPS)

| Scenario | Success Rate | p50 Latency | p99 Latency | Status |
|----------|-------------|-------------|-------------|--------|
| get_products | 0% | 30s | 30s timeout | ❌ FAIL |
| get_product_by_id | 17.7% | 20.2s | ~29s | ❌ FAIL |
| create_product | 22.8% | 20.6s | ~30s | ❌ FAIL |
| get_categories | 19% | 20.6s | ~30s | ❌ FAIL |
| get_orders | 0% | 30s | 30s timeout | ❌ FAIL |

#### Realistic Workflow Scenarios (60s duration)

| Scenario | RPS | Success Rate | p50 Latency | p99 Latency | Status |
|----------|-----|-------------|-------------|-------------|--------|
| browse_and_review | 5 | 0% | 30.0s | 30.2s | ❌ FAIL |
| create_order_workflow | 3 | 0% | 30.0s | 59.3s | ❌ FAIL |
| user_registration_and_browse | 2 | 0% | 30.0s | 59.4s | ❌ FAIL |

#### Stress Test Scenarios

| Scenario | Load Pattern | Success Rate | Status |
|----------|-------------|--------------|--------|
| stress_get_products | Ramp to 50 RPS, sustain 60s | 0% | ❌ FAIL |
| spike_test | Spike to 100 RPS | 0% | ❌ FAIL |
| constant_load | 10 concurrent users | 0% | ❌ FAIL |
| mixed_operations_stress | Ramp to 30 RPS | 0% | ❌ FAIL |

#### Error Distribution

| Error Type | Count | Percentage |
|-----------|-------|------------|
| **Operation Timeout** (>30s) | 11,805 | 97.4% |
| **Connection Refused** | 156 | 1.3% |
| **Internal Server Error** | 12 | 0.1% |
| **Successful** | 144 | 1.2% |

### Key Observations

**Micro-Benchmarks:**
- Cold start penalty of ~180-193ms for application initialization
- Single product retrieval shows acceptable isolated performance (5ms mean)
- GetAllProducts (15,000 rows) exhibits high variance (145-563ms) and massive memory allocation (50MB)
- Memory allocations scale linearly with result set size

**Load Tests:**
- API cannot sustain even 10 RPS under concurrent load
- Database query latency degrades from 10ms → 1,000ms+ as load increases
- API crashes completely under load (connection refused errors indicate server failure)
- No recovery observed - degradation is permanent until restart

**Performance Cliff:**
- First ~50 requests show acceptable performance
- Sharp degradation occurs after connection pool saturation
- Cascading failures in multi-step workflows (browse → review workflows timeout entirely)

### Baseline Summary

The non-optimized API demonstrates critical performance issues:

- **Throughput**: Cannot sustain 10 RPS (target: 1,000+ RPS)
- **Latency**: p99 > 30 seconds (target: < 200ms)
- **Reliability**: 98.8% failure rate under load (target: > 99.9% success)
- **Memory**: 50MB allocation for 15K row query suggests inefficient serialization
- **Stability**: Complete API failure under sustained load

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
