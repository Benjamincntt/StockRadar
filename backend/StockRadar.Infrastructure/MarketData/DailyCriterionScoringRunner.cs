using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

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
    IOptions<PriceRunupFilterOptions> runupFilter,
    IOptions<SmartMoneyOptions> smartMoneyOptions,
    ILogger<DailyCriterionScoringRunner> logger) : IDailyCriterionScoringService
{
    public async Task<int> RunAfterAnalysisAsync(CancellationToken cancellationToken = default)
    {
        var generatedAt = DateTime.UtcNow;
        var all = await stocks.GetAllAsync(cancellationToken);
        var index = await marketIndex.GetCurrentAsync(cancellationToken);
        var runup = runupFilter.Value.ToSettings();
        var sm = smartMoneyOptions.Value.ToSettings();
        var context = smartMoney.BuildContext(all, index, runup, sm);

        var oppDate = await opportunities.GetLatestForDateAsync(cancellationToken);
        var oppSymbols = oppDate.HasValue
            ? (await opportunities.GetSymbolsForDateAsync(oppDate.Value, cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var scoredStocks = all
            .Where(s => s.History.Count >= 2)
            .OrderByDescending(s => oppSymbols.Contains(s.Symbol))
            .ThenBy(s => s.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (scoredStocks.Count == 0)
        {
            logger.LogWarning("Chấm điểm tiêu chí: không có mã đủ lịch sử.");
            return 0;
        }

        var asOfDate = scoredStocks[0].History[^2].Date;
        var hits = new Dictionary<CriterionType, int>();
        var totals = new Dictionary<CriterionType, int>();
        var scoreSums = new Dictionary<CriterionType, decimal>();
        var groupHits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var groupTotals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var groupScoreSums = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var stockRecords = new List<StockCriterionScoreRecord>();
        var detailRecords = new List<StockCriterionDetailRecord>();
        var weights = await criterionRepo.GetWeightsAsync(cancellationToken);

        foreach (var stock in scoredStocks)
        {
            var historyT1 = stock.History.Take(stock.History.Count - 1).ToList();
            if (historyT1.Count < 2)
                continue;

            var stockT1 = CloneWithHistory(stock, historyT1);
            var nextChange = ComputeNextDayChange(stock.History);

            var patternScores = indicatorAnalyzer.ScoreIndicators(stockT1);
            var smartScores = smartMoneyScorer.ScoreCriteria(stockT1, context);
            var allScores = patternScores.Concat(smartScores).ToList();

            foreach (var score in allScores)
            {
                var group = CriterionLabels.GetGroup(score.Type);
                totals.TryGetValue(score.Type, out var t);
                totals[score.Type] = t + 1;
                scoreSums.TryGetValue(score.Type, out var sum);
                scoreSums[score.Type] = sum + score.Score;

                groupTotals.TryGetValue(group, out var gt);
                groupTotals[group] = gt + 1;
                groupScoreSums.TryGetValue(group, out var gs);
                groupScoreSums[group] = gs + score.Score;

                var matched = accuracyEval.MatchesOutcome(score.Bias, score.Score, nextChange);
                if (matched)
                {
                    hits.TryGetValue(score.Type, out var h);
                    hits[score.Type] = h + 1;
                    groupHits.TryGetValue(group, out var gh);
                    groupHits[group] = gh + 1;
                }

                detailRecords.Add(new StockCriterionDetailRecord(
                    asOfDate,
                    stock.Symbol,
                    score.Type,
                    group,
                    CriterionLabels.GetRank(score.Type),
                    score.Score,
                    score.Bias,
                    score.Summary,
                    nextChange,
                    matched));
            }

            if (oppSymbols.Contains(stock.Symbol) || allScores.Any(p => p.Score >= 60))
            {
                var composite = accuracyEval.ComputeCompositeScore(allScores, weights);
                stockRecords.Add(new StockCriterionScoreRecord(
                    asOfDate,
                    stock.Symbol,
                    composite,
                    nextChange,
                    allScores));
            }
        }

        var snapshots = totals
            .Select(kv =>
            {
                var type = kv.Key;
                var total = kv.Value;
                var hit = hits.GetValueOrDefault(type);
                var pct = total > 0 ? Math.Round((decimal)hit / total * 100m, 1) : 0m;
                var avg = total > 0 ? Math.Round(scoreSums.GetValueOrDefault(type) / total, 1) : 0m;
                return new CriterionAccuracySnapshot(type, hit, total, pct, avg);
            })
            .ToList();

        var groupSnapshots = groupTotals
            .Select(kv =>
            {
                var total = kv.Value;
                var hit = groupHits.GetValueOrDefault(kv.Key);
                var pct = total > 0 ? Math.Round((decimal)hit / total * 100m, 1) : 0m;
                var avg = total > 0 ? Math.Round(groupScoreSums.GetValueOrDefault(kv.Key) / total, 1) : 0m;
                var criterionCount = snapshots.Count(s => CriterionLabels.GetGroup(s.Type) == kv.Key);
                return new CriterionGroupAccuracySnapshot(kv.Key, hit, total, pct, avg, criterionCount);
            })
            .ToList();

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
                var action = CriterionReviewHelper.Recommend(r.AccuracyPercent, r.TotalCount);
                var isActive = action != CriterionReviewAction.Remove;
                var weight = CriterionReviewHelper.ComputeWeight(r.AccuracyPercent, r.TotalCount, isActive);
                rolling30Map.TryGetValue(r.Type, out var r30);
                return new CriterionWeight(
                    r.Type,
                    weight,
                    r.AccuracyPercent,
                    r.TotalCount,
                    r30?.AccuracyPercent ?? r.AccuracyPercent,
                    isActive,
                    action);
            })
            .ToList();

        if (newWeights.Count > 0)
            await criterionRepo.UpsertWeightsAsync(newWeights, cancellationToken);

        var weekStart = CriterionReviewHelper.GetWeekStart(asOfDate);
        var weeklyCriteria = rolling7
            .Select(r =>
            {
                var w = newWeights.FirstOrDefault(x => x.Type == r.Type);
                var action = w?.RecommendedAction ?? CriterionReviewHelper.Recommend(r.AccuracyPercent, r.TotalCount);
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
                    action != CriterionReviewAction.Remove);
            })
            .OrderBy(c => c.Rank)
            .ToList();

        var weeklyGroups = groupTotals.Keys
            .Select(groupId =>
            {
                var criteriaInGroup = weeklyCriteria.Where(c => c.GroupId == groupId).ToList();
                var hit = groupHits.GetValueOrDefault(groupId);
                var total = groupTotals.GetValueOrDefault(groupId);
                var pct = total > 0 ? Math.Round((decimal)hit / total * 100m, 1) : 0m;
                var avg = total > 0 ? Math.Round(groupScoreSums.GetValueOrDefault(groupId) / total, 1) : 0m;
                var keep = criteriaInGroup.Count(c => c.RecommendedAction == CriterionReviewAction.Keep);
                var watch = criteriaInGroup.Count(c => c.RecommendedAction == CriterionReviewAction.Watch);
                var remove = criteriaInGroup.Count(c => c.RecommendedAction == CriterionReviewAction.Remove);
                var action = CriterionReviewHelper.RecommendGroup(pct, total);
                return new CriterionGroupWeeklySnapshot(
                    groupId, hit, total, pct, avg, keep, watch, remove, action);
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
            "Chấm điểm T-1 ({AsOf}): {Details} chi tiết, {Stocks} mã tổng hợp, tuần {Week}",
            asOfDate,
            detailRecords.Count,
            stockRecords.Count,
            weekStart);

        return stockRecords.Count;
    }

    private static decimal ComputeNextDayChange(IReadOnlyList<OhlcvBar> history)
    {
        var prev = history[^2].Close;
        var last = history[^1].Close;
        if (prev <= 0)
            return 0;
        return Math.Round((last - prev) / prev * 100m, 2);
    }

    private static Stock CloneWithHistory(Stock stock, IReadOnlyList<OhlcvBar> history) =>
        stock with { History = history.ToList() };
}
