using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using Microsoft.Extensions.Options;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>Gom delta cùng mã trong cửa sổ 1–3 phút thành một sự kiện lô lớn.</summary>
internal sealed class TradeEventAggregator(IOptions<OpportunityMonitorOptions> options)
{
    private readonly object _gate = new();
    private readonly Dictionary<string, SymbolBucket> _buckets =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<AggregatedTradeEvent> Add(TradeEventDetector.DetectedTradeEvent scan)
    {
        var cfg = options.Value;
        var now = DateTime.UtcNow;
        var results = new List<AggregatedTradeEvent>();

        lock (_gate)
        {
            ExpireBuckets(now, cfg, results);

            if (scan.IsImmediateBlock)
            {
                _buckets.Remove(scan.Symbol);
                results.Add(ToAggregated(scan, isAggregated: false));
                return results;
            }

            if (!_buckets.TryGetValue(scan.Symbol, out var bucket))
            {
                bucket = new SymbolBucket { StartedAt = now };
                _buckets[scan.Symbol] = bucket;
            }

            bucket.Scans.Add(scan);

            if (TryBuildFromBucket(scan.Symbol, bucket, cfg, isAggregated: true) is { } aggregated)
            {
                results.Add(aggregated);
                _buckets.Remove(scan.Symbol);
            }
        }

        return results;
    }

    public IReadOnlyList<AggregatedTradeEvent> FlushExpired()
    {
        var cfg = options.Value;
        var now = DateTime.UtcNow;
        var results = new List<AggregatedTradeEvent>();

        lock (_gate)
            ExpireBuckets(now, cfg, results);

        return results;
    }

    private void ExpireBuckets(
        DateTime now,
        OpportunityMonitorOptions cfg,
        List<AggregatedTradeEvent> results)
    {
        var window = TimeSpan.FromSeconds(Math.Max(60, cfg.AggregateWindowSeconds));
        var expired = _buckets
            .Where(kv => now - kv.Value.StartedAt >= window)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var symbol in expired)
        {
            var bucket = _buckets[symbol];
            if (TryBuildFromBucket(symbol, bucket, cfg, isAggregated: true) is { } aggregated)
                results.Add(aggregated);
            _buckets.Remove(symbol);
        }
    }

    private static AggregatedTradeEvent? TryBuildFromBucket(
        string symbol,
        SymbolBucket bucket,
        OpportunityMonitorOptions cfg,
        bool isAggregated)
    {
        if (bucket.Scans.Count == 0)
            return null;

        var totalVol = bucket.Scans.Sum(s => s.Volume);
        var totalValue = bucket.Scans.Sum(s => s.ValueVnd);
        if (totalVol < cfg.MinTradeVolume || totalValue < cfg.MinTradeValueVnd)
            return null;

        var first = bucket.Scans[0];
        var last = bucket.Scans[^1];
        var netPrice = last.Price - first.Price;
        var spreadPct = first.Price > 0
            ? Math.Round(Math.Abs(netPrice) / first.Price * 100m, 3)
            : 0m;
        var label = TradeEventDetector.ClassifyLabel(spreadPct, netPrice, cfg);

        return new AggregatedTradeEvent(
            symbol,
            label,
            last.Price,
            totalVol,
            totalValue,
            spreadPct,
            last.BookImbalance,
            bucket.Scans.Sum(s => s.ForeignNetDelta),
            bucket.Scans.Sum(s => s.PropDelta),
            isAggregated);
    }

    private static AggregatedTradeEvent ToAggregated(
        TradeEventDetector.DetectedTradeEvent scan,
        bool isAggregated) =>
        new(
            scan.Symbol,
            scan.Label,
            scan.Price,
            scan.Volume,
            scan.ValueVnd,
            scan.SpreadPct,
            scan.BookImbalance,
            scan.ForeignNetDelta,
            scan.PropDelta,
            isAggregated);

    private sealed class SymbolBucket
    {
        public DateTime StartedAt;
        public List<TradeEventDetector.DetectedTradeEvent> Scans = [];
    }
}

internal sealed record AggregatedTradeEvent(
    string Symbol,
    string Label,
    decimal Price,
    long Volume,
    decimal ValueVnd,
    decimal SpreadPct,
    long BookImbalance,
    long ForeignNetDelta,
    long PropDelta,
    bool IsAggregated);
