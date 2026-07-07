using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Enums;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Domain.Services;
using StockRadar.Domain.Services.OpportunityRanking;

namespace StockRadar.Application.Services;

public sealed class OpportunityRankingDatasetService(
    ISetupTrackRepository tracks,
    IOptions<OpportunityRankerOptions> rankerOptions,
    IOptions<OpportunityPerformanceOptions> performanceOptions) : IOpportunityRankingDatasetService
{
    public async Task<OpportunityRankingDatasetDto> BuildAsync(
        int days = 180,
        CancellationToken cancellationToken = default)
    {
        var lookback = Math.Clamp(days, 30, 365);
        var today = TradingCalendar.TodayVietnam();
        var fromDate = TradingSessionMath.SubtractTradingSessions(today, lookback);
        var perf = performanceOptions.Value;
        var maxMae = rankerOptions.Value.MaxAdverseExcursionPercent;

        var rows = await tracks.GetMeasuredOpportunitiesSinceAsync(fromDate, cancellationToken);
        var datasetRows = new List<OpportunityRankingRowDto>();

        foreach (var t in rows)
        {
            if (t.OpportunityRank is null or <= 0)
                continue;

            var (label, labelSource) = ResolveLabel(t, perf.SuccessThresholdPercent, maxMae);
            var input = OpportunityRankFeatures.FromTrack(
                t.OpportunityScore,
                t.PredictedHitPercent,
                t.SetupDna,
                t.TradeState);
            var (path, phase, sectorRank) = OpportunityRankFeatures.ParseSetupDna(t.SetupDna);
            Enum.TryParse<StockTradeState>(t.TradeState, ignoreCase: true, out var ts);

            datasetRows.Add(new OpportunityRankingRowDto(
                t.Symbol,
                t.EntryDate,
                t.OpportunityRank,
                input.BuyScore,
                input.PredictedHitPercent,
                sectorRank > 0 ? sectorRank : input.SectorRank,
                input.RelativeStrength5d,
                input.VolumeRatio,
                ts == StockTradeState.Actionable,
                path == OpportunityRankFeatures.SetupPathKind.Breakout,
                path == OpportunityRankFeatures.SetupPathKind.Shakeout,
                phase == OpportunityRankFeatures.MarketPhaseKind.Favorable,
                label,
                labelSource,
                t.ForwardReturnPercent,
                t.MaxFavorableExcursionPercent,
                t.MaxAdverseExcursionPercent,
                t.TradeState,
                t.SetupDna));
        }

        var positives = datasetRows.Count(r => r.LabelHit);
        var posRate = datasetRows.Count > 0
            ? Math.Round(100m * positives / datasetRows.Count, 1)
            : 0m;

        return new OpportunityRankingDatasetDto(
            fromDate,
            datasetRows.Count > 0 ? datasetRows.Max(r => r.EntryDate) : today,
            datasetRows.Count,
            positives,
            posRate,
            OpportunityRankFeatures.Names,
            datasetRows,
            $"Y=1: MFE≥{perf.SuccessThresholdPercent:0.#}% & MAE>{maxMae:0.#}% (nếu có swing) hoặc Outcome=Good T+2.5.");
    }

    public string ToCsv(OpportunityRankingDatasetDto dataset)
    {
        var header = string.Join(',',
            "symbol", "entry_date", "rank", "buy_score", "predicted_hit", "sector_rank",
            "rs5d", "volume_ratio", "is_actionable", "dna_breakout", "dna_shakeout", "market_favorable",
            "label_hit", "label_source", "forward_return_t25", "mfe", "mae", "trade_state", "setup_dna");

        var lines = new List<string> { header };
        foreach (var r in dataset.Rows)
        {
            lines.Add(string.Join(',',
                Csv(r.Symbol),
                r.EntryDate.ToString("yyyy-MM-dd"),
                r.Rank?.ToString() ?? "",
                r.BuyScore,
                r.PredictedHitPercent.ToString(System.Globalization.CultureInfo.InvariantCulture),
                r.SectorRank,
                r.RelativeStrength5d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                r.VolumeRatio.ToString(System.Globalization.CultureInfo.InvariantCulture),
                r.IsActionable ? 1 : 0,
                r.DnaBreakout ? 1 : 0,
                r.DnaShakeout ? 1 : 0,
                r.MarketFavorable ? 1 : 0,
                r.LabelHit ? 1 : 0,
                Csv(r.LabelSource),
                r.ForwardReturnT25?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                r.MaxFavorableExcursionPercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                r.MaxAdverseExcursionPercent?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
                Csv(r.TradeState ?? ""),
                Csv(r.SetupDna ?? "")));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static (bool Label, string Source) ResolveLabel(
        SetupTrackRecord track,
        decimal successThreshold,
        decimal maxAdverse)
    {
        if (track.SwingMetricsMeasured
            && track.MaxFavorableExcursionPercent.HasValue
            && track.MaxAdverseExcursionPercent.HasValue)
        {
            var hit = track.MaxFavorableExcursionPercent.Value >= successThreshold
                && track.MaxAdverseExcursionPercent.Value > maxAdverse;
            return (hit, "mfe_mae");
        }

        return (track.OutcomeBucket == "Good", "t25_bucket");
    }

    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
