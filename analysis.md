# Offset Pagination Anomaly Analysis

## Benchmark Results Comparison

| Metric | Page1 | Page100 | Page250 | Expected Behavior |
|--------|-------|---------|---------|-------------------|
| **Mean** | 15.642 ms | 8.064 ms | 9.072 ms | Page1 should be fastest |
| **Min** | 6.159 ms ✓ | 7.194 ms | 8.781 ms | Page1 IS fastest (min) |
| **Max** | 21.590 ms ⚠️ | 9.320 ms | 9.451 ms | Page1 has 2.3x higher spikes |
| **StdDev** | 4.3044 ms ⚠️ | 0.5610 ms | 0.2249 ms | Page1 has 7.7x more variance |
| **TotalIssues/Op** | 8,832,205 ⚠️ | 5,641,830 | 7,951,223 | Page1 has 57% more CPU work |
| **CacheMisses/Op** | 386,561 ⚠️ | 206,871 | 208,205 | Page1 has 87% more cache misses |

## Key Findings

1. **Best case performance is correct**: Page1's minimum (6.159ms) < Page100 (7.194ms) < Page250 (8.781ms) ✓
2. **Worst case shows anomaly**: Page1 has occasional 21ms spikes, much higher than other pages
3. **High variance**: Page1's standard deviation (4.3ms) is 7.7x higher than Page100 (0.56ms)
4. **More CPU work**: Page1 executes 57% more CPU instructions than Page100
5. **Cache inefficiency**: Page1 has 87% more L1 cache misses

## Root Cause Hypothesis

The **minimum times** follow the expected pattern, but **occasional spikes** on Page1 inflate the mean.

Possible causes:
1. **Query plan compilation**: First query might trigger PostgreSQL plan compilation
2. **Cold statistics**: Database statistics not warmed for page=1 queries
3. **TotalCount query overhead**: The compiled query `GetProductCount()` might have cold cache issues
4. **Different query plans**: PostgreSQL might optimize differently for OFFSET 0 vs OFFSET 4950

Let me investigate...
