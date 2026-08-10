using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

public sealed class VipAlertAccuracyQueryService(
    IVipAlertFireRepository fires,
    ISetupTrackRepository tracks,
    IOptions<OpportunityPerformanceOptions> performanceOptions) : IVipAlertAccuracyQueryService
{
    public async Task<VipAlertAccuracyReportDto> GetReportAsync(
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        var lookback = Math.Clamp(days, 7, 365);
        var today = TradingCalendar.TodayVietnam();
        var from = TradingSessionMath.SubtractTradingSessions(today, lookback);
        var successThreshold = performanceOptions.Value.SuccessThresholdPercent;

        var rows = await fires.GetSinceAsync(from, cancellationToken);
        var measured = rows.Where(r => r.IntradayMeasured && r.IntradayReturnPercent.HasValue).ToList();
        var avgRet = measured.Count > 0
            ? Math.Round(measured.Average(r => r.IntradayReturnPercent!.Value), 2)
            : 0m;
        var intradayHits = measured.Count(r => r.IntradayReturnPercent!.Value >= successThreshold);
        var intradayHitRate = measured.Count > 0
            ? Math.Round(100m * intradayHits / measured.Count, 1)
            : 0m;

        // Join T+2.5 qua SetupTrack master BuyPoint cùng symbol+entryDate.
        var buyTracks = await tracks.GetAlertHistoryTracksAsync(
            buyPointsOnly: true,
            sourceType: null,
            cancellationToken);
        var masterBuy = buyTracks
            .Where(t => t.OutcomeMeasured && t.EntryDate >= from)
            .ToList();

        var fireKeys = rows
            .Select(r => (r.Symbol, r.SessionDate, r.Signal))
            .ToHashSet();
        var matchedMaster = masterBuy
            .Where(t => fireKeys.Contains((t.Symbol, t.EntryDate, t.SourceType)))
            .ToList();
        var masterHits = matchedMaster.Count(t =>
            string.Equals(t.OutcomeBucket, "Good", StringComparison.OrdinalIgnoreCase));
        var masterHitRate = matchedMaster.Count > 0
            ? Math.Round(100m * masterHits / matchedMaster.Count, 1)
            : 0m;

        return new VipAlertAccuracyReportDto(
            from,
            today,
            rows.Count,
            measured.Count,
            avgRet,
            intradayHitRate,
            matchedMaster.Count,
            masterHitRate,
            Bucket(rows, r => string.IsNullOrWhiteSpace(r.Branch) ? "unknown" : r.Branch!, successThreshold, matchedMaster),
            Bucket(rows, r => string.IsNullOrWhiteSpace(r.MarketPhase) ? "Neutral" : r.MarketPhase!, successThreshold, matchedMaster),
            Bucket(rows, r => MlBucket(r.MlProbAtFire), successThreshold, matchedMaster),
            $"Intraday hit = return ≥ {successThreshold:0.#}% cuối phiên; T+2.5 join SetupTrack master BuyPoint cùng symbol+ngày.");
    }

    private static IReadOnlyList<VipAlertBucketMetricsDto> Bucket(
        IReadOnlyList<VipAlertFireRecord> rows,
        Func<VipAlertFireRecord, string> keySelector,
        decimal successThreshold,
        IReadOnlyList<SetupTrackRecord> matchedMaster)
    {
        return rows
            .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var measured = g.Where(r => r.IntradayMeasured && r.IntradayReturnPercent.HasValue).ToList();
                var hits = measured.Count(r => r.IntradayReturnPercent!.Value >= successThreshold);
                var avg = measured.Count > 0
                    ? Math.Round(measured.Average(r => r.IntradayReturnPercent!.Value), 2)
                    : 0m;
                var keys = g.Select(r => (r.Symbol, r.SessionDate, r.Signal)).ToHashSet();
                var master = matchedMaster.Where(t => keys.Contains((t.Symbol, t.EntryDate, t.SourceType))).ToList();
                var mHits = master.Count(t =>
                    string.Equals(t.OutcomeBucket, "Good", StringComparison.OrdinalIgnoreCase));
                return new VipAlertBucketMetricsDto(
                    g.Key,
                    g.Count(),
                    measured.Count,
                    measured.Count > 0 ? Math.Round(100m * hits / measured.Count, 1) : 0m,
                    avg,
                    master.Count,
                    master.Count > 0 ? Math.Round(100m * mHits / master.Count, 1) : null);
            })
            .ToList();
    }

    private static string MlBucket(decimal? mlProb) => mlProb switch
    {
        null => "na",
        < 40m => "lt40",
        < 50m => "40-50",
        < 60m => "50-60",
        < 70m => "60-70",
        _ => "70plus",
    };
}
