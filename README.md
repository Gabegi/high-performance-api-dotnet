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

### Who Is This For?

- Developers building high-performance APIs
- Teams optimizing existing .NET applications
- Anyone learning performance engineering in .NET

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
