using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Services;

public sealed class CriterionScoringService(
    ICriterionScoringRepository repo,
    ITechnicalIndicatorAnalyzer indicatorAnalyzer,
    ICriterionAccuracyEvaluator accuracyEvaluator,
    Microsoft.Extensions.Options.IOptions<CriterionAccuracyOptions> accuracyOptions) : ICriterionScoringService
{
    public async Task<CriteriaSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var acc = accuracyOptions.Value;
        var horizon = Math.Max(1, acc.ForwardSessions);
        var asOf = await repo.GetLatestAccuracyDateAsync(horizon, cancellationToken);
        if (asOf is null)
        {
            return new CriteriaSummaryDto(
                null,
                null,
                null,
                [],
                [],
                [],
                [],
                "Chưa có dữ liệu — chạy Job 2 + phân tích sau phiên giao dịch.");
        }

        // Đủ snapshot trong 7 ngày thì dùng cửa sổ 7 ngày, không thì dùng RollingDays cấu hình.
        var snapshotDays = await repo.CountAccuracyDatesAsync(
            asOf.Value.AddDays(-7), asOf.Value, horizon, cancellationToken: cancellationToken);
        var rollingDays = snapshotDays >= 5 ? 7 : Math.Max(1, acc.RollingDays);
        var fromRolling = asOf.Value.AddDays(-rollingDays);
        var rollingWindowDays = await repo.CountAccuracyDatesAsync(
            fromRolling, asOf.Value, horizon, cancellationToken: cancellationToken);
        var rollingRaw = await repo.GetAccuracyRollingAsync(fromRolling, asOf.Value, horizon, cancellationToken: cancellationToken);
        var rolling = rollingRaw.Select(EnrichSnapshot).ToList();
        var rollingMap = rolling.ToDictionary(r => r.Type);

        var daily = await repo.GetDailyAccuracyAsync(asOf.Value, horizon, cancellationToken: cancellationToken);
        var groups = await repo.GetGroupDailyAccuracyAsync(asOf.Value, horizon, cancellationToken: cancellationToken);
        var weightDetails = await repo.GetWeightDetailsAsync(cancellationToken);
        var weightMap = weightDetails.ToDictionary(w => w.Type);
        var horizonMap = await BuildHorizonMapAsync(rollingDays, cancellationToken);

        var weekStart = CriterionReviewHelper.GetWeekStart(asOf.Value);

        var criteria = daily
            .Select(c =>
            {
                rollingMap.TryGetValue(c.Type, out var roll);
                var snap = roll ?? EnrichSnapshot(c);
                return ToAccuracyDto(snap, weightMap, rollingMap, horizonMap, rollingWindowDays);
            })
            .OrderByDescending(c => c.ReliabilityScore > 0 ? c.ReliabilityScore : c.AccuracyPercent)
            .ThenBy(c => c.Rank)
            .ToList();

        var weeklyGroupsDb = await repo.GetGroupWeeklyReviewsAsync(weekStart, cancellationToken);

        var groupDtos = weeklyGroupsDb.Count > 0
            ? weeklyGroupsDb.Select(ToGroupWeeklyDto).OrderByDescending(g => g.AccuracyPercent).ToList()
            : BuildGroupDtosFromDaily(groups, criteria);

        var weeklyReview = rolling
            .OrderBy(r => CriterionLabels.GetRank(r.Type))
            .Select(r =>
            {
                weightMap.TryGetValue(r.Type, out var w);
                var action = CriterionReviewHelper.RecommendReliability(
                    r.ReliabilityScore,
                    r.EdgePercent,
                    r.TotalCount,
                    rollingWindowDays);
                return new WeeklyCriterionReviewDto(
                    r.Type.ToString(),
                    CriterionLabels.GetVi(r.Type),
                    CriterionLabels.GetGroup(r.Type),
                    CriterionLabels.GetRank(r.Type),
                    r.HitCount,
                    r.TotalCount,
                    r.AccuracyPercent,
                    r.AvgScore,
                    w?.Weight ?? 1m,
                    action.ToString(),
                    action != CriterionReviewAction.Remove,
                    r.ReliabilityScore,
                    r.EdgePercent,
                    r.AvgMfePercent,
                    r.InvalidationRatePercent,
                    MapBuckets(r.Buckets),
                    MapPhases(r.Phases));
            })
            .ToList();

        var topStocks = await BuildTopStocksAsync(asOf.Value, cancellationToken);

        var horizonNote = acc.ExtraHorizons.Length > 0
            ? $" · thêm khung T+{string.Join("/T+", acc.ExtraHorizons)}"
            : "";
        var primaryHorizon = horizon == 2 ? "T+2.5" : $"T+{horizon}";
        return new CriteriaSummaryDto(
            asOf,
            weekStart,
            null,
            criteria,
            groupDtos,
            weeklyReview,
            topStocks,
            $"Setup trend {primaryHorizon}{horizonNote} · điểm ≥{acc.MinScoreForEvaluation} · MFE ≥{acc.SwingTargetPercent:0.#}% · RS vs VN · đáy nền còn nguyên · reliability = hit + edge + MFE − invalidation.");
    }

    /// <summary>Rolling accuracy của các khung bổ sung (T+10/T+20) theo tiêu chí.</summary>
    private async Task<IReadOnlyDictionary<CriterionType, List<CriterionHorizonDto>>> BuildHorizonMapAsync(
        int rollingDays,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<CriterionType, List<CriterionHorizonDto>>();
        foreach (var horizon in accuracyOptions.Value.ExtraHorizons.Distinct().OrderBy(h => h))
        {
            var latest = await repo.GetLatestAccuracyDateAsync(horizon, cancellationToken);
            if (latest is null)
                continue;

            var rows = await repo.GetAccuracyRollingAsync(
                latest.Value.AddDays(-rollingDays), latest.Value, horizon, cancellationToken);
            foreach (var row in rows)
            {
                var snap = EnrichSnapshot(row);
                if (!map.TryGetValue(snap.Type, out var list))
                {
                    list = [];
                    map[snap.Type] = list;
                }

                list.Add(new CriterionHorizonDto(
                    horizon,
                    snap.HitCount,
                    snap.TotalCount,
                    snap.AccuracyPercent,
                    snap.EdgePercent,
                    snap.AvgMfePercent));
            }
        }

        return map;
    }

    public IReadOnlyList<CriterionScoreDto> ScoreIndicatorsLive(IReadOnlyList<OhlcvBar> history) =>
        indicatorAnalyzer.ScoreIndicators(history).Select(ToScoreDto).ToList();

    private async Task<IReadOnlyList<CriterionStockRankDto>> BuildTopStocksAsync(
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var rows = await repo.GetTopStockScoresAsync(asOf, 15, cancellationToken);
        return rows.Select(r => new CriterionStockRankDto(
            r.Symbol,
            r.CompositeScore,
            r.Scores
                .OrderByDescending(s => s.Score)
                .Take(3)
                .Select(ToScoreDto)
                .ToList())).ToList();
    }

    private static CriterionAccuracyDto ToAccuracyDto(
        CriterionAccuracySnapshot c,
        IReadOnlyDictionary<CriterionType, CriterionWeight> weights,
        IReadOnlyDictionary<CriterionType, CriterionAccuracySnapshot> rolling,
        IReadOnlyDictionary<CriterionType, List<CriterionHorizonDto>> horizons,
        int rollingWindowDays)
    {
        weights.TryGetValue(c.Type, out var w);
        rolling.TryGetValue(c.Type, out var roll);
        horizons.TryGetValue(c.Type, out var horizonList);
        var action = CriterionReviewHelper.RecommendReliability(
            c.ReliabilityScore,
            c.EdgePercent,
            roll?.TotalCount ?? c.TotalCount,
            rollingWindowDays);
        return new(
            c.Type.ToString(),
            CriterionLabels.GetVi(c.Type),
            CriterionLabels.GetGroup(c.Type),
            CriterionLabels.GetRank(c.Type),
            c.HitCount,
            c.TotalCount,
            c.AccuracyPercent,
            c.AvgScore,
            w?.Weight ?? 1m,
            roll?.AccuracyPercent ?? c.AccuracyPercent,
            w?.Accuracy30d ?? c.AccuracyPercent,
            action.ToString(),
            action != CriterionReviewAction.Remove,
            c.ReliabilityScore,
            c.EdgePercent,
            c.AvgMfePercent,
            c.InvalidationRatePercent,
            c.BaselinePercent,
            MapBuckets(c.Buckets),
            MapPhases(c.Phases),
            horizonList);
    }

    private static IReadOnlyList<CriterionBucketDto>? MapBuckets(
        IReadOnlyList<CriterionScoreBucketStats>? buckets) =>
        buckets?.Select(b => new CriterionBucketDto(b.BucketId, b.HitCount, b.TotalCount, b.AccuracyPercent)).ToList();

    private static IReadOnlyList<CriterionPhaseDto>? MapPhases(
        IReadOnlyList<CriterionPhaseStats>? phases) =>
        phases?.Select(p => new CriterionPhaseDto(p.Phase.ToString(), p.HitCount, p.TotalCount, p.AccuracyPercent)).ToList();

    private static List<CriterionGroupAccuracyDto> BuildGroupDtosFromDaily(
        IReadOnlyList<CriterionGroupAccuracySnapshot> groups,
        IReadOnlyList<CriterionAccuracyDto> criteria) =>
        groups.Select(g =>
        {
            var inGroup = criteria.Where(c => string.Equals(c.Group, g.GroupId, StringComparison.Ordinal)).ToList();
            var keep = inGroup.Count(c => c.RecommendedAction == CriterionReviewAction.Keep.ToString());
            var watch = inGroup.Count(c => c.RecommendedAction == CriterionReviewAction.Watch.ToString());
            var remove = inGroup.Count(c => c.RecommendedAction == CriterionReviewAction.Remove.ToString());
            var reliability = g.ReliabilityScore > 0 ? g.ReliabilityScore : g.AccuracyPercent;
            var edge = g.EdgePercent;
            if (edge == 0 && inGroup.Count > 0)
                edge = Math.Round(inGroup.Average(c => c.EdgePercent), 1);
            var action = CriterionReviewHelper.RecommendGroup(reliability, g.TotalCount);
            return new CriterionGroupAccuracyDto(
                g.GroupId,
                g.HitCount,
                g.TotalCount,
                g.AccuracyPercent,
                g.AvgScore,
                g.CriterionCount,
                action.ToString(),
                keep,
                watch,
                remove,
                reliability,
                edge);
        })
        .OrderByDescending(g => g.ReliabilityScore > 0 ? g.ReliabilityScore : g.AccuracyPercent)
        .ToList();

    private static CriterionGroupAccuracyDto ToGroupWeeklyDto(CriterionGroupWeeklySnapshot g) => new(
        g.GroupId,
        g.HitCount,
        g.TotalCount,
        g.AccuracyPercent,
        g.AvgScore,
        g.KeepCount + g.WatchCount + g.RemoveCount,
        g.RecommendedAction.ToString(),
        g.KeepCount,
        g.WatchCount,
        g.RemoveCount,
        0,
        0);

    private CriterionAccuracySnapshot EnrichSnapshot(CriterionAccuracySnapshot snap)
    {
        if (snap.TotalCount <= 0)
            return snap;

        var edge = snap.EdgePercent != 0
            ? snap.EdgePercent
            : Math.Round(snap.AccuracyPercent - snap.BaselinePercent, 1);
        var reliability = snap.ReliabilityScore > 0
            ? snap.ReliabilityScore
            : accuracyEvaluator.ComputeReliabilityScore(
                snap.AccuracyPercent,
                edge,
                snap.AvgMfePercent,
                snap.InvalidationRatePercent);

        return snap with { EdgePercent = edge, ReliabilityScore = reliability };
    }

    public async Task<ReliabilityBacktestDto> BacktestReliabilityWeightsAsync(
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        var acc = accuracyOptions.Value;
        var horizon = Math.Max(1, acc.ForwardSessions);
        var latest = await repo.GetLatestAccuracyDateAsync(horizon, cancellationToken: cancellationToken);
        if (latest is null)
            return new ReliabilityBacktestDto(days, 0, 0, 0, [], null, "Chưa có snapshot accuracy nào.");

        var from = latest.Value.AddDays(-Math.Clamp(days, 7, 120));
        var series = await repo.GetDailyAccuracySeriesAsync(from, latest.Value, horizon, cancellationToken: cancellationToken);
        var dates = series.Select(p => p.AsOfDate).Distinct().OrderBy(d => d).ToList();
        if (dates.Count < 4)
        {
            return new ReliabilityBacktestDto(
                days, dates.Count, 0, 0, [],
                null,
                $"Cần ≥4 ngày snapshot để chia train/test — hiện có {dates.Count}. Chạy backfill trước.");
        }

        var splitIdx = dates.Count / 2;
        var trainDates = dates.Take(splitIdx).ToHashSet();
        var testDates = dates.Skip(splitIdx).ToHashSet();

        var train = AggregateByCriterion(series.Where(p => trainDates.Contains(p.AsOfDate)));
        var test = AggregateByCriterion(series.Where(p => testDates.Contains(p.AsOfDate)));

        // Chỉ so tiêu chí có đủ mẫu ở cả hai nửa.
        var common = train.Keys
            .Where(t => test.ContainsKey(t) && train[t].Total >= 20 && test[t].Total >= 20)
            .ToList();
        if (common.Count < 5)
        {
            return new ReliabilityBacktestDto(
                days, trainDates.Count, testDates.Count, common.Count, [],
                null,
                $"Chỉ {common.Count} tiêu chí đủ mẫu ở cả train lẫn test (cần ≥5).");
        }

        var candidates = new (string Name, decimal Hit, decimal Edge, decimal Mfe, decimal Intact)[]
        {
            ("hiện tại", acc.ReliabilityHitWeight, acc.ReliabilityEdgeWeight, acc.ReliabilityMfeWeight, acc.ReliabilityBaseIntactWeight),
            ("nặng hit", 0.6m, 0.2m, 0.1m, 0.1m),
            ("nặng edge", 0.2m, 0.5m, 0.2m, 0.1m),
            ("cân bằng", 0.25m, 0.25m, 0.25m, 0.25m),
        };

        var testEdge = common.Select(t => test[t].Edge).ToList();
        var results = candidates
            .Select(c =>
            {
                var reliability = common
                    .Select(t => ComputeReliability(train[t], c.Hit, c.Edge, c.Mfe, c.Intact))
                    .ToList();
                return new ReliabilityWeightCandidateDto(
                    c.Name, c.Hit, c.Edge, c.Mfe, c.Intact,
                    Math.Round(SpearmanCorrelation(reliability, testEdge), 3));
            })
            .ToList();

        var best = results.OrderByDescending(r => r.RankCorrelation).First();
        return new ReliabilityBacktestDto(
            days,
            trainDates.Count,
            testDates.Count,
            common.Count,
            results,
            best.Name,
            "Correlation Spearman giữa reliability (nửa đầu) và edge thực tế (nửa sau); càng gần 1 càng dự báo tốt. "
            + "Chỉnh trọng số qua config CriterionAccuracy:Reliability*Weight nếu một bộ vượt trội ổn định nhiều tuần.");
    }

    private sealed record CriterionAggregate(int Hits, int Total, decimal HitRate, decimal Edge, decimal Mfe, decimal Invalidation);

    private static Dictionary<CriterionType, CriterionAggregate> AggregateByCriterion(
        IEnumerable<CriterionAccuracyDailyPoint> points) =>
        points
            .GroupBy(p => p.Snapshot.Type)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var hits = g.Sum(p => p.Snapshot.HitCount);
                    var total = g.Sum(p => p.Snapshot.TotalCount);
                    var hitRate = total > 0 ? (decimal)hits / total * 100m : 0m;
                    decimal Weighted(Func<CriterionAccuracySnapshot, decimal> sel) =>
                        total > 0 ? g.Sum(p => sel(p.Snapshot) * p.Snapshot.TotalCount) / total : 0m;
                    var baseline = Weighted(s => s.BaselinePercent);
                    return new CriterionAggregate(
                        hits,
                        total,
                        Math.Round(hitRate, 1),
                        Math.Round(hitRate - baseline, 1),
                        Weighted(s => s.AvgMfePercent),
                        Weighted(s => s.InvalidationRatePercent));
                });

    private static decimal ComputeReliability(
        CriterionAggregate a, decimal hitW, decimal edgeW, decimal mfeW, decimal intactW)
    {
        var edgeNorm = Math.Clamp((a.Edge + 10m) * 5m, 0m, 100m);
        var mfeNorm = Math.Clamp(a.Mfe / 5m * 100m, 0m, 100m);
        var intactNorm = Math.Clamp(100m - a.Invalidation, 0m, 100m);
        return hitW * a.HitRate + edgeW * edgeNorm + mfeW * mfeNorm + intactW * intactNorm;
    }

    private static decimal SpearmanCorrelation(IReadOnlyList<decimal> a, IReadOnlyList<decimal> b)
    {
        var ranksA = ToRanks(a);
        var ranksB = ToRanks(b);
        var n = a.Count;
        var meanA = ranksA.Average();
        var meanB = ranksB.Average();
        decimal cov = 0, varA = 0, varB = 0;
        for (var i = 0; i < n; i++)
        {
            var da = ranksA[i] - meanA;
            var db = ranksB[i] - meanB;
            cov += da * db;
            varA += da * da;
            varB += db * db;
        }

        if (varA == 0 || varB == 0)
            return 0;
        return cov / (decimal)Math.Sqrt((double)(varA * varB));
    }

    private static decimal[] ToRanks(IReadOnlyList<decimal> values)
    {
        var indexed = values
            .Select((v, i) => (Value: v, Index: i))
            .OrderBy(x => x.Value)
            .ToList();
        var ranks = new decimal[values.Count];
        for (var i = 0; i < indexed.Count; i++)
            ranks[indexed[i].Index] = i + 1;
        return ranks;
    }

    internal static CriterionScoreDto ToScoreDto(CriterionScore s) => new(
        s.Type.ToString(),
        CriterionLabels.GetVi(s.Type),
        CriterionLabels.IsBundle(s.Type)
            ? CriterionLabels.GetBundleComponents(s.Type)
            : CriterionLabels.GetGroup(s.Type),
        CriterionLabels.GetRank(s.Type),
        s.Score,
        s.Bias.ToString(),
        s.Summary);
}
