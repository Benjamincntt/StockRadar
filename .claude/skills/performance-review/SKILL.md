---
name: performance-review
description: Review or design for performance in .NET, EF Core, SQL Server, pipeline jobs, and web/mobile clients. Use for slow endpoints, N+1 queries, missing pagination, SQL index and query-plan review, JSON column scans, caching, async and allocation hot paths, payload size, job throughput, and any change labeled optimization.
---

# Performance Review

## Start

Performance claims need comparable numbers. Define the hot path, realistic universe size and concurrency, environment, measurement tool, and acceptable threshold. An optimization without a before and after baseline is unverified.

## Checklist

### EF Core and application

- Prevent N+1, client-side filtering, unbounded lists, and entity-heavy projections.
- Use `AsNoTracking`, projection, pagination and caps, supporting indexes, short transactions, and bounded batches where appropriate.
- Avoid blocking async calls, serialized independent I/O, repeated remote work in loops, unbounded retries, and oversized payloads.
- Define cache keys, lifetime, invalidation, stampede behavior, and fallback.

### StockRadar hot paths

- `Stocks.HistoryJson` is a large JSON column over the active universe. Parsing it with `OPENJSON` across all rows is the single most expensive pattern in this repo. Prefer the persisted summary columns (`LastClose`, `LastVolume`, `LastChangePercent`) when they answer the question; if JSON parsing is unavoidable, bound it by symbol or by tail slice and report the measured cost.
- Pipeline job throughput matters as much as endpoint latency: universe scan, append, daily analysis, and the 15-minute intraday Top refresh run on a clock. State per-symbol and total wall time, not just a query time.
- Endpoints consumed by mobile over mobile networks are payload-sensitive. Report response size alongside latency.
- The intraday window (9:00–11:30, 13:00–14:45) is the peak. A change that is fine at night can still miss the refresh interval.

### Measurement

1. Capture baseline command, dataset, environment, warm-up, sample count, percentile or query plan, and resource sizes.
2. Apply the smallest justified change.
3. Repeat the same measurement and report absolute and percentage delta.
4. For browser work, include applicable Core Web Vitals, navigation timing, failed or large resources, and viewport.
5. Reference a durable baseline only when repeated trend tracking is requested; label stale or incomparable runs.
6. Define a regression threshold for release-critical paths.

## Output

```markdown
| Severity | Path / Query | Baseline | Current | Delta | Evidence | Recommendation / Threshold |
| --- | --- | --- | --- | --- | --- | --- |
```

Also report data-volume assumptions, tool and command, environmental limitations, index and cache decisions, and next measurement.

## Quality Gate

Block unbounded list or query growth, per-row query loops, full-universe JSON scans without a measured justification, cache without invalidation, or an optimization without comparable evidence. A skipped or incomparable measurement is `Unknown`, not improved.
