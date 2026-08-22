using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Mapping;
using StockRadar.Application.Options;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

public sealed class StockService(
    IJobStockRepository jobStocks,
    IDailyOpportunityRepository dailyOpportunities,
    SmartMoneyEvaluationService smartMoneyEval,
    IBuyDecisionEngine buyDecision,
    ISignalAnalyzer signalAnalyzer,
    ISignalFormatter formatter,
    IChartBarProvider chartBars,
    ITechnicalIndicatorAnalyzer indicatorAnalyzer,
    ISmartMoneyCriterionScorer opportunityScorer,
    ICriterionScoringRepository criterionRepo,
    ICriterionAccuracyEvaluator accuracyEval,
    IOptions<PriceRunupFilterOptions> runupFilter) : IStockService
{
    private const int MaxHistoryBarsInDetail = 250;

    public async Task<StockDetailDto?> GetDetailAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var match = await jobStocks.GetBySymbolAsync(symbol, cancellationToken);
        if (match is null)
            return null;

        var context = await smartMoneyEval.BuildContextAsync(cancellationToken);
        var decision = buyDecision.Evaluate(match, context);
        var buyDecisionDto = DtoMapper.ToDto(decision);
        var runupSettings = runupFilter.Value.ToSettings();
        var lichSuChamDiem = signalAnalyzer.LayLichSuChamDiem(match);
        var stockChamDiem = match with { History = lichSuChamDiem };
        var flatBox = signalAnalyzer.AnalyzeFlatBox(lichSuChamDiem, runupSettings);
        var levels = signalAnalyzer.CalculatePriceLevels(lichSuChamDiem);
        var activeSignals = signalAnalyzer
            .DetectSignals(stockChamDiem, context.Index.ChangePercent, runupSettings)
            .Select(t => t == SignalType.DarvasBreakout && flatBox.HasValidBox
                ? BasePriceLabels.FormatSignalTitle(match.Symbol, flatBox, match.LatestPrice)
                : formatter.FormatTitle(t, match.Symbol))
            .ToList();

        var summary = decision.Reasons.Count > 0
            ? string.Join(". ", decision.Reasons) + "."
            : decision.PassesTopFilter
                ? $"{match.Symbol} đạt điều kiện SmartMoney."
                : decision.GateFailure ?? $"{match.Symbol} chưa đạt điều kiện SmartMoney.";

        var patternScores = indicatorAnalyzer.ScoreIndicators(match);
        var opportunityScores = opportunityScorer.ScoreCriteria(match, context);
        var weights = await criterionRepo.GetWeightsAsync(cancellationToken);
        var singles = patternScores.Where(s => CriterionLabels.IsIndicator(s.Type)).ToList();
        var bundles = patternScores.Where(s => CriterionLabels.IsBundle(s.Type)).ToList();
        var patternComposite = accuracyEval.ComputeCompositeScore(singles, weights);
        var bundleComposite = bundles.Count > 0
            ? accuracyEval.ComputeCompositeScore(bundles, weights)
            : 0;

        // Buy Score canonical: snapshot Top cơ hội nếu có, không thì on-the-fly (cùng engine).
        var targetDate = TradingCalendar.GetActiveOpportunityDate();
        var snap = await dailyOpportunities.GetBySymbolAsync(match.Symbol, targetDate, cancellationToken);
        var displayBuyScore = decision.BuyScore;
        DateTime? buyScoreAsOf = null;
        var buyScoreSource = "live";
        if (snap?.BuyScore is int snapshotBuy)
        {
            displayBuyScore = snapshotBuy;
            buyScoreAsOf = snap.GeneratedAt;
            buyScoreSource = "snapshot";

            // Đồng bộ chuỗi gate/reason với snapshot — tránh "Buy Score 29 < 62" khi pill đã là 50,
            // và tránh câu tự mâu thuẫn "Buy Score 82 < 62" khi điểm snapshot đã vượt ngưỡng.
            var liveGate = buyDecisionDto.GateFailure;
            var gateFailure = SyncBuyScoreGateWithSnapshot(liveGate, snap.TradeStateReason, snapshotBuy);

            // Headline điểm vào do AlignEntryWithTopGate chép nguyên gate live — chép lại theo
            // chuỗi đã đồng bộ, nếu không hai thẻ hiện hai con điểm khác nhau.
            var entryPoint = buyDecisionDto.EntryPoint;
            if (!string.IsNullOrEmpty(liveGate)
                && string.Equals(entryPoint.Headline, liveGate, StringComparison.Ordinal)
                && !string.Equals(gateFailure, liveGate, StringComparison.Ordinal))
            {
                entryPoint = entryPoint with { Headline = gateFailure ?? TopGateFallbackHeadline };
            }

            buyDecisionDto = buyDecisionDto with
            {
                BuyScore = snapshotBuy,
                GateFailure = gateFailure,
                EntryPoint = entryPoint,
                TradeStateReason = snap.TradeStateReason ?? buyDecisionDto.TradeStateReason,
            };
        }

        var opportunityComposite = displayBuyScore;
        var allCriterionDtos = patternScores
            .Concat(opportunityScores)
            .Select(CriterionScoringService.ToScoreDto)
            .OrderBy(p => p.Rank)
            .ToList();
        var historyDto = match.History
            .Where(b => TradingSessionMath.IsTradingDay(b.Date))
            .Skip(Math.Max(0, match.History.Count(b => TradingSessionMath.IsTradingDay(b.Date)) - MaxHistoryBarsInDetail))
            .Select(DtoMapper.ToDto)
            .ToList();

        return new StockDetailDto(
            match.Symbol,
            match.Name,
            match.Sector,
            match.LatestPrice,
            signalAnalyzer.GetChangePercent(match, 1),
            displayBuyScore,
            $"{decision.SectorWave.WaveLabel} — {decision.SectorWave.BreadthDetail}",
            decision.PassesTopFilter,
            decision.Reasons,
            summary,
            activeSignals,
            levels.BuyZone,
            levels.StopLoss,
            levels.Resistance,
            levels.Target,
            decision.RelativeStrength5d,
            decision.VolumeRatio,
            historyDto,
            DtoMapper.ToDto(flatBox, runupSettings.MaxGainFromBasePercent, match.LatestPrice),
            allCriterionDtos,
            patternComposite,
            bundleComposite,
            opportunityComposite,
            buyDecisionDto.EntryPoint,
            buyDecisionDto,
            buyScoreAsOf,
            buyScoreSource,
            match.IsActive,
            match.TradingStatus,
            match.UniverseUpdatedAt);
    }

    public async Task<StockChartDto?> GetChartAsync(
        string symbol,
        string interval,
        CancellationToken cancellationToken = default)
    {
        var sym = symbol.Trim().ToUpperInvariant();
        var stock = await jobStocks.GetBySymbolAsync(sym, cancellationToken);
        if (stock is null)
            return null;

        var normalized = NormalizeInterval(interval);
        if (!chartBars.IsSupportedInterval(normalized))
            return null;

        var bars = await chartBars.FetchAsync(sym, normalized, cancellationToken);

        if (normalized.Equals("1D", StringComparison.OrdinalIgnoreCase))
        {
            var dbBars = stock.History
                .Where(b => TradingSessionMath.IsTradingDay(b.Date))
                .Select(b => new ChartBarDto(
                    b.Date.ToString("yyyy-MM-dd"),
                    b.Open,
                    b.High,
                    b.Low,
                    b.Close,
                    b.Volume))
                .ToList();

            if (dbBars.Count > bars.Count)
                bars = dbBars;
        }
        else if (bars.Count == 0)
        {
            bars = stock.History
                .Select(b => new ChartBarDto(
                    b.Date.ToString("yyyy-MM-dd"),
                    b.Open,
                    b.High,
                    b.Low,
                    b.Close,
                    b.Volume))
                .ToList();
        }

        return new StockChartDto(sym, normalized, bars);
    }

    private const string BuyScoreGatePrefix = "Buy Score ";
    private const string TopGateFallbackHeadline = "Chưa đạt đủ điều kiện Top cơ hội";

    /// <summary>
    /// Gate "Buy Score x &lt; y" tính trên điểm live, còn pill hiển thị điểm snapshot.
    /// Đồng bộ hai số; nếu điểm snapshot đã ≥ ngưỡng thì cổng điểm không còn hiệu lực —
    /// trả lý do snapshot (hoặc null) thay vì câu vô nghĩa "Buy Score 82 &lt; 62".
    /// </summary>
    private static string? SyncBuyScoreGateWithSnapshot(
        string? liveGate,
        string? snapshotReason,
        int snapshotScore)
    {
        if (string.IsNullOrWhiteSpace(liveGate)
            || !liveGate.StartsWith(BuyScoreGatePrefix, StringComparison.Ordinal))
            return liveGate;

        if (!string.IsNullOrWhiteSpace(snapshotReason)
            && snapshotReason.StartsWith(BuyScoreGatePrefix, StringComparison.Ordinal))
            return snapshotReason;

        var parts = liveGate.Split('<', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return liveGate;

        if (int.TryParse(parts[1], out var threshold) && snapshotScore >= threshold)
            return string.IsNullOrWhiteSpace(snapshotReason) ? null : snapshotReason;

        return $"{BuyScoreGatePrefix}{snapshotScore} < {parts[1]}";
    }

    private static string NormalizeInterval(string interval)
    {
        var value = interval.Trim();
        return value.ToUpperInvariant() switch
        {
            "1D" => "1D",
            "1H" => "1H",
            "30M" => "30m",
            "15M" => "15m",
            "5M" => "5m",
            "1M" => "1m",
            _ => value
        };
    }
}
