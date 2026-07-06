using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Mapping;
using StockRadar.Application.Options;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

public sealed class StockService(
    IJobStockRepository jobStocks,
    SmartMoneyEvaluationService smartMoneyEval,
    IBuyDecisionEngine buyDecision,
    ISignalAnalyzer signalAnalyzer,
    ISignalFormatter formatter,
    IChartBarProvider chartBars,
    ITechnicalIndicatorAnalyzer indicatorAnalyzer,
    ISmartMoneyCriterionScorer opportunityScorer,
    ICriterionScoringRepository criterionRepo,
    ICriterionAccuracyEvaluator accuracyEval,
    ISwingDecisionService swingDecision,
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
        var swing = await swingDecision.BuildAsync(decision, context, match.Symbol, cancellationToken);
        var buyDecisionDto = DtoMapper.ToDto(decision, swing);
        var runupSettings = runupFilter.Value.ToSettings();
        var flatBox = signalAnalyzer.AnalyzeFlatBox(match.History, runupSettings);
        var levels = signalAnalyzer.CalculatePriceLevels(match.History);
        var activeSignals = signalAnalyzer
            .DetectSignals(match, context.Index.ChangePercent, runupSettings)
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
        var opportunityComposite = decision.BuyScore;
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
            decision.BuyScore,
            decision.SectorRank,
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
            buyDecisionDto);
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
