# ApexShop Micro Benchmarks

Performance benchmarks for ApexShop API using BenchmarkDotNet.

## Prerequisites

1. **Docker Engine** must be running
2. **PostgreSQL database** must be running:
   ```bash
   docker-compose up -d
   ```
3. **Administrator privileges** (Windows) for full diagnostics
   - Run Command Prompt or PowerShell as Administrator
   - Or comment out `[HardwareCounters(...)]` attribute in `ApiEndpointBenchmarks.cs:23-29`

## Running Benchmarks

```bash
dotnet run -c Release --project ApexShop.Benchmarks.Micro
```

**Important:** Always run in Release mode for accurate results.

## Diagnostics Enabled

### MemoryDiagnoser
**What it shows:**
- Heap memory allocations per operation
- Gen 0/1/2 garbage collections
- Bytes allocated
- Allocation rate

**Use for:** Finding memory leaks, reducing allocations, optimizing GC pressure

### ThreadingDiagnoser
**What it shows:**
- Lock contention time
- Thread pool usage
- Thread context switches
- Completed work items

**Use for:** Identifying threading bottlenecks, async/await issues

### ExceptionDiagnoser
**What it shows:**
- Exception frequency
- Exception types thrown
- Performance impact of exceptions

**Use for:** Finding hot exception paths, control flow via exceptions

### EventPipeProfiler (CPU Sampling)
**What it shows:**
- Time breakdown by component:
  - HTTP transport (network I/O)
  - JSON serialization/deserialization
  - Database queries
  - Middleware (routing, logging, auth)
  - Business logic
- CPU hot paths
- Method-level time distribution

**Use for:** Understanding where time is spent, finding optimization opportunities

**Note:** Adds ~10% overhead to measurements but provides valuable insights

### HardwareCounters
**What it shows:**
- Branch mispredictions
- CPU cache misses (L1, L2, LLC)
- Total CPU cycles
- CPU instructions issued

**Use for:** Cache optimization, CPU-level performance tuning

**Requirements:**
- ⚠️ Administrator privileges on Windows
- Run terminal/VS as Admin or comment out this attribute
- Most useful when implementing caching strategies

## Metrics Displayed

- **Mean:** Average execution time
- **Median:** Middle value (50th percentile)
- **Min/Max:** Fastest and slowest iterations
- **Rank:** Performance ranking among benchmarks
- **Allocated:** Memory allocated per operation
- **Gen 0/1/2:** Garbage collection counts

## Reports

Results are saved to: `ApexShop.Benchmarks.Micro/Reports/`

Formats:
- HTML (detailed interactive report)
- CSV (raw data for analysis)

## Baseline (Non-Optimized Results)

**Test System:** Intel Core i7-8650U @ 1.90GHz (4 cores, 8 threads), .NET 9.0.9

### Performance Summary

| Rank | Endpoint | Mean | Median | Allocated | Ratio vs Baseline |
|------|----------|------|--------|-----------|-------------------|
| 🥇 1 | **Api_GetSingleProduct** | 5.1ms | 4.8ms | 84.41 KB | 1.00x (baseline) |
| 🥈 2 | **Api_ColdStart** | 180.2ms | 181.1ms | 4,405.5 KB | 36.18x |
| 🥉 3 | **Api_GetAllProducts** | 417.3ms | 462.6ms | 50,585.34 KB | 83.77x |
| 🏁 - | **Api_TrueColdStart** | 193.0ms | 193.0ms | 4,492.42 KB | Single run |

### Detailed Metrics

**Api_GetSingleProduct (Baseline)**
- ✅ Median: 4.8ms (Range: 4.0ms - 7.0ms)
- ✅ Allocated: 84 KB
- ✅ Cache Misses: ~224K
- ⚠️ High variance - needs investigation

**Api_GetAllProducts (15,000 records)**
- 🔴 Median: 463ms (Range: 145ms - 563ms)
- 🔴 Allocated: 50.6 MB (599x baseline)
- 🔴 Work Items: 314 (high threading overhead)
- 🔴 Cache Misses: ~12.2M (54x baseline)
- ⚠️ Extreme variance indicates performance issues

**Cold Start Performance**
- Api_TrueColdStart: 193ms (first-ever request)
- Api_ColdStart: 180ms average (15 iterations)
- Allocated: ~4.4 MB during startup

### Issues Identified

1. **GetAllProducts Memory**: 50 MB allocation for single request
   - Likely materializing entire 15K dataset
   - Needs pagination implementation
   - Consider streaming/chunked responses

2. **High Variance**: GetAllProducts ranges 145ms-563ms
   - Indicates GC pressure from allocations
   - Database query optimization needed

3. **Single Product Variance**: 4-7ms range
   - Possible connection pooling issues
   - Query optimization opportunity

### Hardware Insights

- **Lock Contentions**: Minimal (0-1.3) - good concurrent design
- **Cache Efficiency**: Single product performs well, GetAllProducts has poor cache utilization
- **Branch Predictions**: Scales proportionally with data volume

## Current Benchmarks

### Api_ColdStart
Tests average API startup performance across multiple iterations (15 runs). Useful for containerized environments with frequent restarts.

### Api_TrueColdStart
Single-iteration cold start measurement (no warmup). Shows first-request performance after deployment. Only runs once to measure genuine cold start time.

### Api_GetSingleProduct (Baseline)
Measures single product retrieval performance with warm application. Used as baseline for comparison.

### Api_GetAllProducts
Measures performance of listing all products with category includes.

## Best Practices

1. **Close other applications** before running
2. **Run multiple times** - first run is always slower (JIT compilation)
3. **Disable antivirus scanning** on project folder temporarily
4. **Run on AC power** (not battery) for consistent results
5. **Keep machine idle** during benchmark runs
6. **Use Release configuration** always

## Interpreting Results

- Focus on **Median** rather than Mean (less affected by outliers)
- Compare **Allocated** memory between approaches
- Watch for **Gen 2** collections (expensive)
- Use **EventPipeProfiler** output to identify bottlenecks
- Check **HardwareCounters** cache misses when optimizing caching
