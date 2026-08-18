using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Domain.Services;
using StockRadar.Domain.Services.OpportunityRanking;

namespace StockRadar.Application.Services;

public sealed class OpportunityRankingDatasetService(
    ISetupTrackRepository tracks,
    IJobStockRepository stockRepo,
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

        // Nạp lịch sử giá để tính real feature value (rs5d, volume_ratio, ATR, dist_ma20).
        var allStocks = await stockRepo.GetAllForUniverseScreeningAsync(cancellationToken);
        var historyMap = allStocks.ToDictionary(s => s.Symbol, s => s.History, StringComparer.OrdinalIgnoreCase);

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

            // Tính real OHLCV features tại ngày entry.
            historyMap.TryGetValue(t.Symbol, out var history);
            var (rs5d, volumeRatio, atrPct, distMa20) = ComputeOhlcvFeatures(history, t.EntryDate);

            datasetRows.Add(new OpportunityRankingRowDto(
                t.Symbol,
                t.EntryDate,
                t.OpportunityRank,
                input.BuyScore,
                input.PredictedHitPercent,
                sectorRank > 0 ? sectorRank : input.SectorWaveRank,
                rs5d,
                volumeRatio,
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
                t.SetupDna,
                AtrPercent: atrPct,
                DistMa20Percent: distMa20));
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
            "symbol", "entry_date", "rank", "buy_score", "predicted_hit", "sector_wave_rank",
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
                r.SectorWaveRank,
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

    /// <summary>
    /// Tính (rs5d%, volumeRatio, atr14%, distMa20%) tại ngày entryDate từ OHLCV history.
    /// Trả về 0/1 nếu không đủ data.
    /// </summary>
    private static (decimal Rs5d, decimal VolumeRatio, decimal AtrPct, decimal DistMa20) ComputeOhlcvFeatures(
        IReadOnlyList<OhlcvBar>? history,
        DateOnly entryDate)
    {
        if (history is null || history.Count < 5)
            return (0m, 1m, 0m, 0m);

        // Tìm index bar tại entryDate (hoặc bar gần nhất trước đó).
        var idx = -1;
        for (var i = history.Count - 1; i >= 0; i--)
        {
            if (history[i].Date <= entryDate)
            {
                idx = i;
                break;
            }
        }

        if (idx < 0)
            return (0m, 1m, 0m, 0m);

        var bar = history[idx];

        // RS5d: thay đổi % so với 5 phiên trước.
        var rs5d = 0m;
        if (idx >= 5 && history[idx - 5].Close > 0)
            rs5d = (bar.Close - history[idx - 5].Close) / history[idx - 5].Close * 100m;

        // Volume ratio: vol hôm nay / avg vol 20 phiên trước.
        var volumeRatio = 1m;
        var avgLen = Math.Min(20, idx);
        if (avgLen > 0)
        {
            var avgVol = 0L;
            for (var i = idx - avgLen; i < idx; i++)
                avgVol += history[i].Volume;
            var avg = (decimal)(avgVol / avgLen);
            if (avg > 0)
                volumeRatio = Math.Min((decimal)bar.Volume / avg, 5m);
        }

        // ATR14 kết thúc tại phiên đang xét — dùng chung công thức toàn hệ thống.
        var atr14 = IndicatorMath.AtrAt(history, idx, 14);
        var atrPct = bar.Close > 0 ? atr14 / bar.Close * 100m : 0m;

        // Dist MA20: (close - ma20) / ma20 * 100%.
        var distMa20 = 0m;
        var ma20Len = Math.Min(20, idx + 1);
        if (ma20Len >= 5)
        {
            var sum = 0m;
            for (var i = idx - ma20Len + 1; i <= idx; i++)
                sum += history[i].Close;
            var ma20 = sum / ma20Len;
            if (ma20 > 0)
                distMa20 = (bar.Close - ma20) / ma20 * 100m;
        }

        return (rs5d, volumeRatio, atrPct, distMa20);
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
