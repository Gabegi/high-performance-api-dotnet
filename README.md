# high-performance-api-dotnet

Optimising Performance to the max for a .NET API

## Overview

A high-performance e-commerce API built with .NET 9 and PostgreSQL, designed to demonstrate production-grade performance optimization techniques. This project serves as a reference implementation for building scalable, low-latency APIs.

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

### Who Is This For?

- Developers building high-performance APIs
- Teams optimizing existing .NET applications
- Anyone learning performance engineering in .NET

## Architecture

This project follows a **Vertical Slice Architecture** with the primary objective of achieving **highest performance**.

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

## Entity Framework Core Performance Optimization Guide

This section documents the systematic approach to optimizing EF Core performance, organized by implementation priority. Each phase builds upon the previous, creating a comprehensive optimization strategy.

### Optimization Progress Tracker

#### ✅ Phase 1.1: Connection Resiliency & Timeout Configuration

**Location:** `ApexShop.Infrastructure/DependencyInjection.cs:13-26`

**Implementation:**
```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
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
        }));
```

**What This Does:**
- **Automatic Retry Logic**: Retries failed database operations up to 3 times with exponential backoff (delays grow progressively up to 5 seconds)
- **Transient Failure Handling**: Automatically handles network blips, connection timeouts, temporary deadlocks, and other transient database errors
- **Explicit Timeout**: Sets a 30-second timeout for all database commands to prevent indefinite hangs

**Performance Impact:**
- **Reliability**: Reduces 500 errors caused by temporary network/database issues without application code changes
- **User Experience**: Failed requests are automatically retried transparently
- **Production-Ready**: Industry-standard resilience pattern for distributed systems
- **Zero Overhead**: Retry logic only activates on failures, no performance cost for successful operations

**Scenarios Handled:**
- Network packet loss between application and database
- Database connection pool exhaustion recovery
- Temporary database unavailability (restarts, failovers)
- Deadlock resolution (PostgreSQL automatically rolls back one transaction, retry succeeds)

---

### Phase 1: Foundation Setup (Implement First)

#### Context & Connection Management

- **DbContext Lifetime** - Keep DbContext short-lived; create per request/operation, not application-wide
- **Dispose Properly** - Always dispose DbContext or use `using` statements to return connections to pool
- **Connection Pooling** - Ensure connection pooling is enabled (default in most providers)
- **Connection Resiliency** - Enable retry logic with `.EnableRetryOnFailure()` for transient failures

#### Database Design Basics

- **Create Indexes** - Index frequently queried columns for faster lookups
- **Specify String Lengths** - Always set `[MaxLength]` or `.HasMaxLength()` instead of using unlimited `nvarchar(max)`
- **Choose Appropriate Data Types** - Use `varchar` over `nvarchar` when Unicode not needed; set appropriate decimal precision
- **Optimize Relationships** - Properly configure cascade delete behaviors; use `OnDelete(DeleteBehavior.NoAction)` to avoid multiple cascade paths
- **Composite Keys** - Carefully consider performance implications; single surrogate keys usually perform better

#### Monitoring Setup (Start Early)

- **Log Generated SQL** - Configure logging to see actual SQL queries generated by EF Core
- **Enable Sensitive Data Logging** - Use `.EnableSensitiveDataLogging()` in development to see parameter values
- **Use Database Profilers** - Tools like SQL Server Profiler, EF Core logging, or MiniProfiler to identify slow queries

### Phase 2: Quick Wins (High Impact, Low Effort)

#### Basic Query Optimization

- **Use AsNoTracking()** - Disable change tracking for read-only operations; can improve performance by up to 60% and significantly reduce memory usage
- **Filter Early** - Apply `.Where()` clauses before materializing data to ensure filtering happens at the database level, not in memory
- **Use Find() for Primary Keys** - Checks DbContext's local cache before querying database, avoiding unnecessary roundtrips
- **Use Any() over Count()** - For existence checks, `Any()` is more efficient than `Count() > 0` as it stops at first match
- **Parameterized Queries** - Always use parameterized queries to enable SQL Server query plan caching and prevent SQL injection

#### Loading Strategy Basics

- **Avoid Lazy Loading** - Causes N+1 problems with multiple database roundtrips for each related entity access
- **Avoid N+1 Problem** - Never query inside foreach loops; load all required data upfront using eager loading to prevent multiple database roundtrips

### Phase 3: Core Query Optimization (Daily Use)

#### Query Optimization Techniques

- **Select Specific Columns** - Use `.Select()` to retrieve only needed fields instead of entire entities
- **IQueryable vs IEnumerable** - Use `IQueryable` for database queries (server-side execution with optimized SQL), `IEnumerable` for in-memory collections (client-side execution)
- **Pagination** - Use `.Skip()` and `.Take()` to avoid loading large datasets
- **Query Tags** - Add `.TagWith("description")` to queries for better diagnostics and performance monitoring in logs
- **Projection** - Map entities to DTOs early using `.Select()` to reduce data transfer
- **Global Query Filters** - Configure filters (soft deletes, multi-tenancy) once in `OnModelCreating` instead of repeating in every query

#### Query Execution Optimization

- **Deferred Execution** - EF Core only executes queries when data is actually needed (e.g., `.ToList()`, `foreach`); use this for performance optimization
- **Buffering vs Streaming** - Use buffering (`.ToList()`) for smaller result sets; use streaming (`foreach`, `.AsEnumerable()`) for large datasets to reduce memory consumption
- **Database Null Semantics** - Enable for queries with many null comparisons to generate simpler, more efficient SQL
- **Compiled Queries** - Pre-compile frequently used queries using `EF.CompileQuery()` or `EF.CompileAsyncQuery()` for 30-50% performance improvement

### Phase 4: Advanced Loading Strategies

#### Loading Strategy Optimization

- **Eager Loading (Recommended)** - Use `.Include()` and `.ThenInclude()` to load related entities in a single query with SQL joins
- **Explicit Loading** - Use `.Load()` to manually trigger loading when needed; better than lazy loading but less optimal than eager loading
- **Split Queries** - Break complex queries with multiple related entities into separate queries to avoid cartesian explosion
- **Avoid Cartesian Explosion** - Be careful with multiple `.Include()` statements; use Split Queries when needed
- **AsSingleQuery vs AsSplitQuery** - Explicitly control query splitting behavior; use `.AsSingleQuery()` for small datasets, `.AsSplitQuery()` to avoid cartesian explosion with multiple collections

### Phase 5: Change Tracking Optimization

#### Change Tracking Management

- **No-Tracking by Default** - Set `ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking` globally for read-heavy applications
- **Clear Change Tracker** - Call `ChangeTracker.Clear()` between operations in long-lived contexts to prevent memory buildup
- **DetectChanges Performance** - Understand that `DetectChanges` is called automatically before `SaveChanges`, queries, and other operations
- **Disable AutoDetectChanges** - Set `ChangeTracker.AutoDetectChangesEnabled = false` during bulk operations, manually call `DetectChanges()` when done

### Phase 6: Write Operation Optimization

#### Write Operations

- **AddRange/UpdateRange/RemoveRange** - Use range methods instead of multiple individual Add/Update/Remove calls
- **Batch SaveChanges()** - Control frequency of save operations to reduce database roundtrips
- **Attach for Updates** - Use `Attach()` with modified state for disconnected scenarios instead of loading entity first
- **ExecuteUpdate/ExecuteDelete (EF Core 7+)** - Perform bulk operations without loading entities into memory
- **Bulk Methods** - Use specialized bulk operation libraries for inserting/updating many records

### Phase 7: Caching Strategies (After Basic Optimization)

#### Caching Implementation

- **DbContext Pooling** - Significantly improves performance in high-traffic scenarios by reusing DbContext instances
- **Memory Cache** - Use `MemoryCache` for frequently accessed query results
- **Second-Level Caching** - Implement using libraries like `EFSecondLevelCache.Core` to cache query results

### Phase 8: Advanced Techniques (When Needed)

#### Advanced Optimization

- **Compiled Models (EF Core)** - Pre-compile models for applications with hundreds/thousands of entity types to reduce startup time
- **Raw SQL** - Use `FromSqlInterpolated` or `FromSqlRaw` for complex queries when LINQ is insufficient
- **Keyless Entity Types** - Use for views, stored procedures, or query results that don't require tracking
- **Owned Entity Types** - For value objects to improve performance and modeling
- **Temporal Tables (EF Core 6+)** - Use for audit history without performance overhead of triggers
- **Table Splitting** - Multiple entities mapping to same table for optimization

#### Advanced Database Design

- **Denormalization** - Sometimes duplicating data reduces joins and improves read performance (trade-off with data consistency)

### Phase 9: Collection Type Optimization (Fine-Tuning)

#### Collection Types

- **ICollection vs List** - Use `ICollection` for EF relationships; `IList` for indexed access and modification

### Implementation Checklist

Track your optimization progress using this checklist:

- [ ] **Phase 1: Foundation Setup**
  - [ ] Configure DbContext lifetime management (per-request scope)
  - [ ] Implement proper disposal patterns
  - [ ] Verify connection pooling configuration
  - [ ] Add connection resiliency with retry logic
  - [ ] Review and optimize database indexes
  - [ ] Set explicit string lengths on all string properties
  - [ ] Optimize data types (varchar vs nvarchar, decimal precision)
  - [ ] Configure cascade delete behaviors
  - [ ] Enable SQL query logging
  - [ ] Set up database profiling tools

- [ ] **Phase 2: Quick Wins**
  - [x] Add `AsNoTracking()` to all read-only queries
  - [ ] Move `.Where()` filters before materialization
  - [x] ~~Replace lookups with `Find()` for primary key searches~~ **Decision: Keep `AsNoTracking().FirstOrDefaultAsync()` for GET operations** - `Find()` cannot work with `AsNoTracking()` as it always enables change tracking. For read-only GET operations, `AsNoTracking()` provides better performance. `Find()` is already correctly used in PUT/DELETE operations where change tracking is needed.
  - [x] Replace `Count() > 0` with `Any()` - No instances found in codebase
  - [x] Verify all queries use parameterization - All queries use EF Core LINQ which auto-parameterizes; no raw SQL found
  - [x] Remove lazy loading configuration - Lazy loading is not enabled; no `.UseLazyLoadingProxies()` and navigation properties are not virtual
  - [x] Identify and fix N+1 query issues - No N+1 issues found; endpoints don't access navigation properties; seeder already optimized

- [ ] **Phase 3: Core Query Optimization**
  - [ ] Use `.Select()` for projection instead of loading full entities
  - [ ] Ensure `IQueryable` is used for database queries
  - [ ] Implement pagination on collection endpoints
  - [ ] Add query tags for diagnostics
  - [ ] Convert entities to DTOs using `.Select()`
  - [ ] Configure global query filters
  - [ ] Implement compiled queries for frequently-used queries

- [ ] **Phase 4: Advanced Loading Strategies**
  - [ ] Replace lazy loading with eager loading (`.Include()`)
  - [ ] Identify queries with cartesian explosion
  - [ ] Apply split queries where appropriate
  - [ ] Configure `AsSingleQuery()` vs `AsSplitQuery()` strategically

- [ ] **Phase 5: Change Tracking Optimization**
  - [ ] Set no-tracking as default for read-heavy applications
  - [ ] Add `ChangeTracker.Clear()` in long-lived contexts
  - [ ] Disable `AutoDetectChanges` during bulk operations

- [ ] **Phase 6: Write Operation Optimization**
  - [ ] Replace individual operations with range methods
  - [ ] Batch `SaveChanges()` calls appropriately
  - [ ] Use `Attach()` for disconnected updates
  - [ ] Implement `ExecuteUpdate`/`ExecuteDelete` for bulk operations

- [ ] **Phase 7: Caching Strategies**
  - [ ] Enable DbContext pooling
  - [ ] Implement memory caching for frequently accessed data
  - [ ] Evaluate second-level caching libraries

- [ ] **Phase 8: Advanced Techniques**
  - [ ] Evaluate compiled models for large entity counts
  - [ ] Identify queries that benefit from raw SQL
  - [ ] Use keyless entity types for views/stored procedures
  - [ ] Implement owned entity types for value objects

- [ ] **Phase 9: Collection Type Optimization**
  - [ ] Review navigation property collection types
  - [ ] Use `ICollection` for EF relationships
