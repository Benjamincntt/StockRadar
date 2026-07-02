using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.Persistence;
using System.Text.Json;

namespace StockRadar.Infrastructure.MarketData;

internal sealed class DailyCriterionScoringRunner(
    IJobStockRepository stocks,
    IJobMarketIndexProvider marketIndex,
    IDailyOpportunityRepository opportunities,
    ICriterionScoringRepository criterionRepo,
    ISmartMoneyOpportunitySelector smartMoney,
    ITechnicalIndicatorAnalyzer indicatorAnalyzer,
    ISmartMoneyCriterionScorer smartMoneyScorer,
    ICriterionAccuracyEvaluator accuracyEval,
    ITrendSetupEvaluator trendSetup,
    ISignalAnalyzer signalAnalyzer,
    AdaptiveScoringProfileFactory adaptiveProfileFactory,
    HitCalibrationProfileFactory hitCalibrationProfileFactory,
    ApplicationDbContext db,
    IOptions<PriceRunupFilterOptions> runupFilter,
    IOptions<SmartMoneyOptions> smartMoneyOptions,
    IOptions<CriterionAccuracyOptions> accuracyOptions,
    ILogger<DailyCriterionScoringRunner> logger) : IDailyCriterionScoringService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<int> RunAfterAnalysisAsync(CancellationToken cancellationToken = default)
    {
        var generatedAt = DateTime.UtcNow;
        var accSettings = accuracyOptions.Value.ToSettings();
        var forward = Math.Max(1, accSettings.ForwardSessions);
        var all = await stocks.GetAllAsync(cancellationToken);
        var index = await marketIndex.GetCurrentAsync(cancellationToken);
        var indexHistory = await LoadIndexHistoryAsync(cancellationToken);
        var runup = runupFilter.Value.ToSettings();
        var sm = smartMoneyOptions.Value.ToSettings();
        var adaptive = await adaptiveProfileFactory.LoadAsync(cancellationToken);
        var calibration = await hitCalibrationProfileFactory.LoadAsync(cancellationToken);
        var context = smartMoney.BuildContext(all, index, runup, sm, adaptive, calibration);

        var oppDate = await opportunities.GetLatestForDateAsync(cancellationToken);
        var oppSymbols = oppDate.HasValue
            ? (await opportunities.GetSymbolsForDateAsync(oppDate.Value, cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var scoredStocks = all
            .Where(s => s.History.Count >= forward + 2)
            .OrderByDescending(s => oppSymbols.Contains(s.Symbol))
            .ThenBy(s => s.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (scoredStocks.Count == 0)
        {
            logger.LogWarning("Chấm điểm tiêu chí: không có mã đủ lịch sử (cần ≥{Min} phiên).", forward + 2);
            return 0;
        }

        var latestSession = scoredStocks.Max(s => s.History[^1].Date);
        var asOfDate = TradingSessionMath.SubtractTradingSessions(latestSession, forward);
        var collector = new CriterionMetricsCollector();
        var stockRecords = new List<StockCriterionScoreRecord>();
        var detailRecords = new List<StockCriterionDetailRecord>();
        var weights = await criterionRepo.GetWeightsAsync(cancellationToken);
        var skippedNoDate = 0;
        var skippedNoForward = 0;
        var skippedTrend = 0;
        var skippedBase = 0;

        foreach (var stock in scoredStocks)
        {
            var stockAsOfIdx = FindStockAsOfIndex(stock.History, asOfDate);
            if (stockAsOfIdx < 0)
            {
                skippedNoDate++;
                continue;
            }

            if (stockAsOfIdx + forward >= stock.History.Count)
            {
                skippedNoForward++;
                continue;
            }

            var historyAtAsOf = stock.History.Take(stockAsOfIdx + 1).ToList();
            if (accSettings.RequireTrendSetup
                && !trendSetup.HasValidTrendSetup(historyAtAsOf, runup, sm))
            {
                skippedTrend++;
                continue;
            }

            var baseProfile = signalAnalyzer.AnalyzeBasePriceForFilter(historyAtAsOf, runup)
                ?? signalAnalyzer.AnalyzeBasePrice(historyAtAsOf, runup);
            if (baseProfile is null)
            {
                skippedBase++;
                continue;
            }

            var baseLow = baseProfile.BaseLow;
            var stockAtAsOf = CloneWithHistory(stock, historyAtAsOf);
            var marketPhase = trendSetup.ClassifyMarketPhase(indexHistory, FindIndexAsOf(indexHistory, asOfDate));

            var patternScores = indicatorAnalyzer.ScoreIndicators(stockAtAsOf);
            var smartScores = smartMoneyScorer.ScoreCriteria(stockAtAsOf, context);
            var allScores = patternScores.Concat(smartScores).ToList();

            var bullishOutcome = trendSetup.MeasureOutcome(
                stock.History,
                stockAsOfIdx,
                forward,
                baseLow,
                PatternBias.Bullish,
                indexHistory,
                accSettings);
            collector.RecordBaseline(bullishOutcome.IsHit);

            foreach (var score in allScores)
            {
                if (!accuracyEval.ShouldEvaluate(score.Score, score.Bias))
                    continue;

                var outcome = trendSetup.MeasureOutcome(
                    stock.History,
                    stockAsOfIdx,
                    forward,
                    baseLow,
                    score.Bias,
                    indexHistory,
                    accSettings);
                var bucket = trendSetup.GetScoreBucket(score.Score);
                var matched = accuracyEval.MatchesOutcome(score.Bias, score.Score, outcome);
                var group = CriterionLabels.GetGroup(score.Type);

                collector.Record(
                    score.Type,
                    group,
                    score.Score,
                    bucket,
                    marketPhase,
                    matched,
                    outcome);

                detailRecords.Add(new StockCriterionDetailRecord(
                    asOfDate,
                    stock.Symbol,
                    score.Type,
                    group,
                    CriterionLabels.GetRank(score.Type),
                    score.Score,
                    score.Bias,
                    score.Summary,
                    outcome.ForwardChangePercent,
                    matched,
                    outcome.MaxFavorablePercent,
                    outcome.MaxAdversePercent,
                    outcome.InvalidatedBase,
                    outcome.RelativeStrengthForward,
                    bucket,
                    marketPhase));
            }

            if (oppSymbols.Contains(stock.Symbol) || allScores.Any(p => p.Score >= 60))
            {
                var composite = accuracyEval.ComputeCompositeScore(allScores, weights);
                stockRecords.Add(new StockCriterionScoreRecord(
                    asOfDate,
                    stock.Symbol,
                    composite,
                    bullishOutcome.ForwardChangePercent,
                    allScores));
            }
        }

        var snapshots = collector.BuildCriterionSnapshots(accuracyEval);
        var groupSnapshots = collector.BuildGroupSnapshots(accuracyEval);

        await criterionRepo.ReplaceDailyAccuracyAsync(asOfDate, snapshots, generatedAt, cancellationToken);
        await criterionRepo.ReplaceGroupDailyAccuracyAsync(asOfDate, groupSnapshots, generatedAt, cancellationToken);
        await criterionRepo.ReplaceStockDetailsAsync(asOfDate, detailRecords, generatedAt, cancellationToken);
        await criterionRepo.ReplaceStockScoresAsync(asOfDate, stockRecords, generatedAt, cancellationToken);

        var from7d = asOfDate.AddDays(-7);
        var from30d = asOfDate.AddDays(-30);
        var rolling7 = await criterionRepo.GetAccuracyRollingAsync(from7d, asOfDate, cancellationToken);
        var rolling30 = await criterionRepo.GetAccuracyRollingAsync(from30d, asOfDate, cancellationToken);
        var rolling30Map = rolling30.ToDictionary(r => r.Type);

        var newWeights = rolling7
            .Select(r =>
            {
                var action = CriterionReviewHelper.RecommendReliability(
                    r.ReliabilityScore,
                    r.EdgePercent,
                    r.TotalCount);
                var isActive = action != CriterionReviewAction.Remove;
                var weight = CriterionReviewHelper.ComputeWeight(r.ReliabilityScore, r.TotalCount, isActive);
                rolling30Map.TryGetValue(r.Type, out var r30);
                return new CriterionWeight(
                    r.Type,
                    weight,
                    r.AccuracyPercent,
                    r.TotalCount,
                    r30?.AccuracyPercent ?? r.AccuracyPercent,
                    isActive,
                    action,
                    r.ReliabilityScore,
                    r.EdgePercent);
            })
            .ToList();

        if (newWeights.Count > 0)
            await criterionRepo.UpsertWeightsAsync(newWeights, cancellationToken);

        var weekStart = CriterionReviewHelper.GetWeekStart(asOfDate);
        var weeklyCriteria = rolling7
            .Select(r =>
            {
                var w = newWeights.FirstOrDefault(x => x.Type == r.Type);
                var action = w?.RecommendedAction
                    ?? CriterionReviewHelper.RecommendReliability(r.ReliabilityScore, r.EdgePercent, r.TotalCount);
                return new WeeklyCriterionReviewSnapshot(
                    r.Type,
                    CriterionLabels.GetGroup(r.Type),
                    CriterionLabels.GetVi(r.Type),
                    CriterionLabels.GetRank(r.Type),
                    r.HitCount,
                    r.TotalCount,
                    r.AccuracyPercent,
                    r.AvgScore,
                    w?.Weight ?? 1m,
                    action,
                    action != CriterionReviewAction.Remove,
                    r.EdgePercent,
                    r.ReliabilityScore,
                    r.AvgMfePercent,
                    r.InvalidationRatePercent,
                    r.Buckets,
                    r.Phases);
            })
            .OrderBy(c => c.Rank)
            .ToList();

        var weeklyGroups = groupSnapshots
            .Select(g =>
            {
                var criteriaInGroup = weeklyCriteria.Where(c => c.GroupId == g.GroupId).ToList();
                var keep = criteriaInGroup.Count(c => c.RecommendedAction == CriterionReviewAction.Keep);
                var watch = criteriaInGroup.Count(c => c.RecommendedAction == CriterionReviewAction.Watch);
                var remove = criteriaInGroup.Count(c => c.RecommendedAction == CriterionReviewAction.Remove);
                var action = CriterionReviewHelper.RecommendGroup(g.ReliabilityScore, g.TotalCount);
                return new CriterionGroupWeeklySnapshot(
                    g.GroupId,
                    g.HitCount,
                    g.TotalCount,
                    g.AccuracyPercent,
                    g.AvgScore,
                    keep,
                    watch,
                    remove,
                    action);
            })
            .OrderByDescending(g => g.AccuracyPercent)
            .ToList();

        await criterionRepo.UpsertWeeklyReviewsAsync(
            weekStart,
            weeklyCriteria,
            weeklyGroups,
            generatedAt,
            cancellationToken);

        logger.LogInformation(
            "Chấm setup T-{Forward} ({AsOf}): {Details} chi tiết, {Stocks} mã, baseline {Baseline:0.#}%, tuần {Week} | bỏ qua: date {SkipDate}, forward {SkipForward}, trend {SkipTrend}, base {SkipBase}",
            forward,
            asOfDate,
            detailRecords.Count,
            stockRecords.Count,
            collector.BaselinePercent,
            weekStart,
            skippedNoDate,
            skippedNoForward,
            skippedTrend,
            skippedBase);

        return stockRecords.Count;
    }

    private async Task<IReadOnlyList<OhlcvBar>> LoadIndexHistoryAsync(CancellationToken cancellationToken)
    {
        var entity = await db.MarketIndices.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Symbol == "VNINDEX", cancellationToken);
        if (entity is null || string.IsNullOrWhiteSpace(entity.HistoryJson))
            return [];

        return JsonSerializer.Deserialize<List<OhlcvBar>>(entity.HistoryJson, JsonOptions) ?? [];
    }

    private static int FindIndexAsOf(IReadOnlyList<OhlcvBar> indexHistory, DateOnly asOfDate)
    {
        for (var i = indexHistory.Count - 1; i >= 0; i--)
        {
            if (indexHistory[i].Date <= asOfDate)
                return i;
        }

        return indexHistory.Count > 0 ? indexHistory.Count - 1 : 0;
    }

    private static int FindStockAsOfIndex(IReadOnlyList<OhlcvBar> history, DateOnly asOfDate)
    {
        for (var i = history.Count - 1; i >= 0; i--)
        {
            if (history[i].Date == asOfDate)
                return i;
        }

        return -1;
    }

    private static Stock CloneWithHistory(Stock stock, IReadOnlyList<OhlcvBar> history) =>
        stock with { History = history.ToList() };
}
