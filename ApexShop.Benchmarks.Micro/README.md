# ApexShop Micro Benchmarks

Performance benchmarks for ApexShop API using BenchmarkDotNet.

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
