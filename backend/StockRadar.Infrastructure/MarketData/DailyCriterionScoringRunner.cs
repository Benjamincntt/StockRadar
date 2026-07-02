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

    /// <summary>Mã không có nền chuẩn: dùng đáy N phiên gần nhất làm mức vô hiệu hóa.</summary>
    private const int FallbackSupportLookbackSessions = 20;

    /// <summary>Đủ số snapshot ngày trong 7 ngày gần nhất thì tự nâng cửa sổ rolling lên 7.</summary>
    private const int FullRollingMinSnapshots = 5;

    /// <summary>Phiên VN chốt ~14:45; trước giờ này nến ngày hiện tại chưa hoàn chỉnh.</summary>
    private static readonly TimeSpan SessionCloseTime = new(15, 0, 0);

    private sealed record ScoringContext(
        IReadOnlyList<Stock> Stocks,
        IReadOnlyList<OhlcvBar> IndexHistory,
        SmartMoneyMarketContext MarketContext,
        BasePriceFilterSettings Runup,
        SmartMoneySettings SmartMoney,
        CriterionAccuracySettings AccSettings,
        HashSet<string> OppSymbols,
        IReadOnlyDictionary<CriterionType, decimal> Weights,
        DateOnly LatestSession);

    private sealed record ScoreDateResult(int DetailCount, int StockCount, decimal BaselinePercent);

    public async Task<int> RunAfterAnalysisAsync(CancellationToken cancellationToken = default)
    {
        var ctx = await LoadContextAsync(cancellationToken);
        if (ctx is null)
            return 0;

        var generatedAt = DateTime.UtcNow;
        var forward = Math.Max(1, ctx.AccSettings.ForwardSessions);
        var asOfDate = TradingSessionMath.SubtractTradingSessions(ctx.LatestSession, forward);

        var result = await ScoreDateAsync(ctx, asOfDate, forward, persistStockScores: true, generatedAt, cancellationToken);

        // Khung bổ sung (T+10/T+20) cho chỉ báo trung hạn — asOf lùi xa hơn tương ứng.
        foreach (var horizon in accuracyOptions.Value.ExtraHorizons.Where(h => h > forward).Distinct())
        {
            var asOfH = TradingSessionMath.SubtractTradingSessions(ctx.LatestSession, horizon);
            await ScoreDateAsync(ctx, asOfH, horizon, persistStockScores: false, generatedAt, cancellationToken);
        }

        await UpdateWeightsAndWeeklyAsync(asOfDate, generatedAt, cancellationToken);
        return result.StockCount;
    }

    public async Task<int> RunBackfillAsync(int days, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadContextAsync(cancellationToken);
        if (ctx is null)
            return 0;

        var generatedAt = DateTime.UtcNow;
        var forward = Math.Max(1, ctx.AccSettings.ForwardSessions);
        var latestAsOf = TradingSessionMath.SubtractTradingSessions(ctx.LatestSession, forward);

        var dates = new List<DateOnly>();
        var cursor = latestAsOf;
        for (var i = 0; i < Math.Clamp(days, 1, 120); i++)
        {
            dates.Add(cursor);
            cursor = TradingSessionMath.SubtractTradingSessions(cursor, 1);
        }

        dates.Reverse();
        var scoredDates = 0;
        foreach (var date in dates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ScoreDateAsync(ctx, date, forward, persistStockScores: true, generatedAt, cancellationToken);
            if (result.DetailCount > 0)
                scoredDates++;

            logger.LogInformation(
                "Backfill tiêu chí {Date}: {Details} chi tiết, {Stocks} mã, baseline {Baseline:0.#}%.",
                date,
                result.DetailCount,
                result.StockCount,
                result.BaselinePercent);
        }

        await UpdateWeightsAndWeeklyAsync(latestAsOf, generatedAt, cancellationToken);
        logger.LogInformation("Backfill tiêu chí xong: {Scored}/{Total} ngày có dữ liệu.", scoredDates, dates.Count);
        return scoredDates;
    }

    private async Task<ScoringContext?> LoadContextAsync(CancellationToken cancellationToken)
    {
        var accSettings = accuracyOptions.Value.ToSettings();
        var forward = Math.Max(1, accSettings.ForwardSessions);
        var all = await stocks.GetAllAsync(cancellationToken);
        var index = await marketIndex.GetCurrentAsync(cancellationToken);
        var indexHistory = await LoadIndexHistoryAsync(cancellationToken);
        var runup = runupFilter.Value.ToSettings();
        var sm = smartMoneyOptions.Value.ToSettings();
        var adaptive = await adaptiveProfileFactory.LoadAsync(cancellationToken);
        var calibration = await hitCalibrationProfileFactory.LoadAsync(cancellationToken);

        // Nến ngày hiện tại chưa đóng cửa (Job 2 chạy giữa phiên) → loại khỏi mọi phép đo.
        var nowVn = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
        if (nowVn.TimeOfDay < SessionCloseTime)
        {
            var incompleteDate = DateOnly.FromDateTime(nowVn);
            all = all
                .Select(s => s.History.Count > 0 && s.History[^1].Date == incompleteDate
                    ? s with { History = s.History.Take(s.History.Count - 1).ToList() }
                    : s)
                .ToList();
            if (indexHistory.Count > 0 && indexHistory[^1].Date == incompleteDate)
                indexHistory = indexHistory.Take(indexHistory.Count - 1).ToList();
        }

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
            return null;
        }

        var weights = await criterionRepo.GetWeightsAsync(cancellationToken);
        var latestSession = scoredStocks.Max(s => s.History[^1].Date);

        return new ScoringContext(
            scoredStocks,
            indexHistory,
            context,
            runup,
            sm,
            accSettings,
            oppSymbols,
            weights,
            latestSession);
    }

    private async Task<ScoreDateResult> ScoreDateAsync(
        ScoringContext ctx,
        DateOnly asOfDate,
        int forward,
        bool persistStockScores,
        DateTime generatedAt,
        CancellationToken cancellationToken)
    {
        var accSettings = ctx.AccSettings;
        var collector = new CriterionMetricsCollector();
        var stockRecords = new List<StockCriterionScoreRecord>();
        var detailRecords = new List<StockCriterionDetailRecord>();
        var skippedNoDate = 0;
        var skippedNoForward = 0;
        var skippedTrend = 0;
        var fallbackBaseCount = 0;
        var marketPhase = trendSetup.ClassifyMarketPhase(ctx.IndexHistory, FindIndexAsOf(ctx.IndexHistory, asOfDate));

        foreach (var stock in ctx.Stocks)
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
                && !trendSetup.HasValidTrendSetup(historyAtAsOf, ctx.Runup, ctx.SmartMoney))
            {
                skippedTrend++;
                continue;
            }

            // Đo thống kê trên toàn universe: mã không có nền chuẩn vẫn được chấm,
            // dùng đáy 20 phiên gần nhất làm mức vô hiệu hóa thay cho đáy nền.
            var baseProfile = signalAnalyzer.AnalyzeBasePriceForFilter(historyAtAsOf, ctx.Runup)
                ?? signalAnalyzer.AnalyzeBasePrice(historyAtAsOf, ctx.Runup);
            decimal baseLow;
            if (baseProfile is not null)
            {
                baseLow = baseProfile.BaseLow;
            }
            else
            {
                fallbackBaseCount++;
                baseLow = historyAtAsOf
                    .Skip(Math.Max(0, historyAtAsOf.Count - FallbackSupportLookbackSessions))
                    .Min(b => b.Low);
            }

            var stockAtAsOf = CloneWithHistory(stock, historyAtAsOf);
            var patternScores = indicatorAnalyzer.ScoreIndicators(stockAtAsOf);
            var smartScores = smartMoneyScorer.ScoreCriteria(stockAtAsOf, ctx.MarketContext);
            var allScores = patternScores.Concat(smartScores).ToList();

            var bullishOutcome = trendSetup.MeasureOutcome(
                stock.History,
                stockAsOfIdx,
                forward,
                baseLow,
                PatternBias.Bullish,
                ctx.IndexHistory,
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
                    ctx.IndexHistory,
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

            if (persistStockScores
                && (ctx.OppSymbols.Contains(stock.Symbol) || allScores.Any(p => p.Score >= 60)))
            {
                var composite = accuracyEval.ComputeCompositeScore(allScores, ctx.Weights);
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

        await criterionRepo.ReplaceDailyAccuracyAsync(asOfDate, forward, snapshots, generatedAt, cancellationToken);
        await criterionRepo.ReplaceGroupDailyAccuracyAsync(asOfDate, forward, groupSnapshots, generatedAt, cancellationToken);
        await criterionRepo.ReplaceStockDetailsAsync(asOfDate, forward, detailRecords, generatedAt, cancellationToken);
        if (persistStockScores)
            await criterionRepo.ReplaceStockScoresAsync(asOfDate, stockRecords, generatedAt, cancellationToken);

        logger.LogInformation(
            "Chấm setup T-{Forward} ({AsOf}): {Details} chi tiết, {Stocks} mã, baseline {Baseline:0.#}%, pha {Phase} | bỏ qua: date {SkipDate}, forward {SkipForward}, trend {SkipTrend} | fallback nền: {FallbackBase}",
            forward,
            asOfDate,
            detailRecords.Count,
            stockRecords.Count,
            collector.BaselinePercent,
            marketPhase,
            skippedNoDate,
            skippedNoForward,
            skippedTrend,
            fallbackBaseCount);

        return new ScoreDateResult(detailRecords.Count, stockRecords.Count, collector.BaselinePercent);
    }

    private async Task UpdateWeightsAndWeeklyAsync(
        DateOnly asOfDate,
        DateTime generatedAt,
        CancellationToken cancellationToken)
    {
        // Tự nâng cửa sổ rolling lên 7 ngày khi đã tích lũy đủ snapshot.
        var snapshotDays = await criterionRepo.CountAccuracyDatesAsync(
            asOfDate.AddDays(-7), asOfDate, cancellationToken: cancellationToken);
        var rollingDays = snapshotDays >= FullRollingMinSnapshots
            ? 7
            : Math.Max(1, accuracyOptions.Value.RollingDays);
        var fromRolling = asOfDate.AddDays(-rollingDays);
        var from30d = asOfDate.AddDays(-30);
        var rollingWindowDays = await criterionRepo.CountAccuracyDatesAsync(
            fromRolling, asOfDate, cancellationToken: cancellationToken);
        var rolling7 = await criterionRepo.GetAccuracyRollingAsync(fromRolling, asOfDate, cancellationToken: cancellationToken);
        var rolling30 = await criterionRepo.GetAccuracyRollingAsync(from30d, asOfDate, cancellationToken: cancellationToken);
        var rolling30Map = rolling30.ToDictionary(r => r.Type);

        var newWeights = rolling7
            .Select(r =>
            {
                var action = CriterionReviewHelper.RecommendReliability(
                    r.ReliabilityScore,
                    r.EdgePercent,
                    r.TotalCount,
                    rollingWindowDays);
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
                    ?? CriterionReviewHelper.RecommendReliability(
                        r.ReliabilityScore, r.EdgePercent, r.TotalCount, rollingWindowDays);
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

        var groupSnapshots = await criterionRepo.GetGroupDailyAccuracyAsync(asOfDate, cancellationToken: cancellationToken);
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
            "Cập nhật trọng số tiêu chí ({AsOf}): rolling {Rolling}d ({Days} ngày snapshot), tuần {Week}.",
            asOfDate,
            rollingDays,
            rollingWindowDays,
            weekStart);
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
