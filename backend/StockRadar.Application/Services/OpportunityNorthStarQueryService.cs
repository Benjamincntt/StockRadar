using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

public sealed class OpportunityNorthStarQueryService(
    ISetupTrackRepository tracks,
    IDailyOpportunityRepository opportunities,
    Microsoft.Extensions.Options.IOptions<OpportunityPerformanceOptions> performanceOptions)
    : IOpportunityNorthStarQueryService
{
    private static readonly (string Id, int MaxRank)[] RankBuckets =
    [
        ("Top3", 3),
        ("Top5", 5),
        ("Top10", 10),
        ("All", int.MaxValue),
    ];

    public async Task<OpportunityNorthStarReportDto> GetReportAsync(
        int days = 90,
        CancellationToken cancellationToken = default)
    {
        var cfg = performanceOptions.Value;
        var lookback = Math.Clamp(days, 14, 180);
        var today = TradingCalendar.TodayVietnam();
        var fromDate = TradingSessionMath.SubtractTradingSessions(today, lookback);

        var measured = await tracks.GetMeasuredOpportunitiesSinceAsync(fromDate, cancellationToken);
        var tradeStateRows = await opportunities.GetTradeStatesSinceAsync(fromDate, cancellationToken);
        var tradeStateMap = tradeStateRows.ToDictionary(
            r => (r.ForTradingDate, r.Symbol),
            r => r.TradeState,
            comparer: DateSymbolComparer.Instance);

        var rankBuckets = RankBuckets
            .Select(b => BuildRankBucket(b.Id, b.MaxRank, measured, cfg.SuccessThresholdPercent))
            .ToList();

        var tradeStateBuckets = BuildTradeStateBuckets(measured, tradeStateMap);

        var toDate = measured.Count > 0
            ? measured.Max(t => t.EntryDate)
            : today;

        return new OpportunityNorthStarReportDto(
            fromDate,
            toDate,
            measured.Count,
            cfg.SuccessThresholdPercent,
            rankBuckets,
            tradeStateBuckets,
            $"Hit T+2.5 = lãi ≥{cfg.SuccessThresholdPercent:0.#}% · {lookback} phiên gần nhất · chỉ setup Opportunity đã đo.");
    }

    private static OpportunityRankBucketMetricsDto BuildRankBucket(
        string bucketId,
        int maxRank,
        IReadOnlyList<SetupTrackRecord> tracks,
        decimal successThreshold)
    {
        var slice = tracks
            .Where(t => t.OpportunityRank is > 0 and var r && r <= maxRank)
            .ToList();
        return ToMetrics(bucketId, maxRank == int.MaxValue ? 0 : maxRank, slice, successThreshold);
    }

    private static IReadOnlyList<OpportunityTradeStateMetricsDto> BuildTradeStateBuckets(
        IReadOnlyList<SetupTrackRecord> tracks,
        IReadOnlyDictionary<(DateOnly Date, string Symbol), string?> tradeStateMap)
    {
        var grouped = tracks
            .GroupBy(t => ResolveTradeState(t, tradeStateMap))
            .OrderByDescending(g => g.Count())
            .ToList();

        return grouped
            .Select(g =>
            {
                var metrics = ToMetrics(g.Key, 0, g.ToList(), 0m);
                return new OpportunityTradeStateMetricsDto(
                    g.Key,
                    LabelTradeState(g.Key),
                    metrics.MeasuredCount,
                    metrics.GoodCount,
                    metrics.HitRatePercent,
                    metrics.AvgReturnT25Percent,
                    metrics.AvgMfePercent,
                    metrics.AvgMaePercent,
                    metrics.SwingSamples);
            })
            .ToList();
    }

    private static string ResolveTradeState(
        SetupTrackRecord track,
        IReadOnlyDictionary<(DateOnly Date, string Symbol), string?> tradeStateMap)
    {
        if (!string.IsNullOrWhiteSpace(track.TradeState))
            return track.TradeState;

        if (tradeStateMap.TryGetValue((track.EntryDate, track.Symbol), out var fromOpp)
            && !string.IsNullOrWhiteSpace(fromOpp))
            return fromOpp;

        return "Unknown";
    }

    private static string LabelTradeState(string tradeState)
    {
        if (Enum.TryParse<StockTradeState>(tradeState, ignoreCase: true, out var parsed))
            return TradeStateLabels.ToVi(parsed);
        return tradeState switch
        {
            "Unknown" => "Chưa ghi nhận",
            _ => tradeState
        };
    }

    private static OpportunityRankBucketMetricsDto ToMetrics(
        string bucketId,
        int maxRank,
        IReadOnlyList<SetupTrackRecord> slice,
        decimal _)
    {
        if (slice.Count == 0)
        {
            return new OpportunityRankBucketMetricsDto(
                bucketId, maxRank, 0, 0, 0, 0, 0, 0, null, null, 0);
        }

        var good = slice.Count(t => t.OutcomeBucket == OutcomeBucket.Good);
        var flat = slice.Count(t => t.OutcomeBucket == OutcomeBucket.Flat);
        var failed = slice.Count(t => t.OutcomeBucket == OutcomeBucket.Failed);
        var returns = slice
            .Where(t => t.ForwardReturnPercent.HasValue)
            .Select(t => t.ForwardReturnPercent!.Value)
            .ToList();
        var swing = slice.Where(t => t.SwingMetricsMeasured).ToList();
        var mfe = swing
            .Where(t => t.MaxFavorableExcursionPercent.HasValue)
            .Select(t => t.MaxFavorableExcursionPercent!.Value)
            .ToList();
        var mae = swing
            .Where(t => t.MaxAdverseExcursionPercent.HasValue)
            .Select(t => t.MaxAdverseExcursionPercent!.Value)
            .ToList();

        return new OpportunityRankBucketMetricsDto(
            bucketId,
            maxRank,
            slice.Count,
            good,
            flat,
            failed,
            Math.Round(100m * good / slice.Count, 1),
            returns.Count > 0 ? Math.Round(returns.Average(), 2) : 0,
            mfe.Count > 0 ? Math.Round(mfe.Average(), 2) : null,
            mae.Count > 0 ? Math.Round(mae.Average(), 2) : null,
            swing.Count);
    }

    private static class OutcomeBucket
    {
        public const string Good = "Good";
        public const string Flat = "Flat";
        public const string Failed = "Failed";
    }

    private sealed class DateSymbolComparer : IEqualityComparer<(DateOnly Date, string Symbol)>
    {
        public static readonly DateSymbolComparer Instance = new();

        public bool Equals((DateOnly Date, string Symbol) x, (DateOnly Date, string Symbol) y) =>
            x.Date == y.Date && x.Symbol.Equals(y.Symbol, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((DateOnly Date, string Symbol) obj) =>
            HashCode.Combine(obj.Date, obj.Symbol.ToUpperInvariant());
    }
}
