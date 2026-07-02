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
        var asOf = await repo.GetLatestAccuracyDateAsync(cancellationToken);
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
            asOf.Value.AddDays(-7), asOf.Value, cancellationToken);
        var rollingDays = snapshotDays >= 5 ? 7 : Math.Max(1, accuracyOptions.Value.RollingDays);
        var fromRolling = asOf.Value.AddDays(-rollingDays);
        var rollingRaw = await repo.GetAccuracyRollingAsync(fromRolling, asOf.Value, cancellationToken);
        var rolling = rollingRaw.Select(EnrichSnapshot).ToList();
        var rollingMap = rolling.ToDictionary(r => r.Type);

        var daily = await repo.GetDailyAccuracyAsync(asOf.Value, cancellationToken);
        var groups = await repo.GetGroupDailyAccuracyAsync(asOf.Value, cancellationToken);
        var weightDetails = await repo.GetWeightDetailsAsync(cancellationToken);
        var weightMap = weightDetails.ToDictionary(w => w.Type);

        var weekStart = CriterionReviewHelper.GetWeekStart(asOf.Value);

        var criteria = daily
            .Select(c =>
            {
                rollingMap.TryGetValue(c.Type, out var roll);
                var snap = roll ?? EnrichSnapshot(c);
                return ToAccuracyDto(snap, weightMap, rollingMap);
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
                    r.TotalCount);
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

        var acc = accuracyOptions.Value;
        return new CriteriaSummaryDto(
            asOf,
            weekStart,
            null,
            criteria,
            groupDtos,
            weeklyReview,
            topStocks,
            $"Setup trend T+{acc.ForwardSessions} · điểm ≥{acc.MinScoreForEvaluation} · MFE ≥{acc.SwingTargetPercent:0.#}% · RS vs VN · đáy nền còn nguyên · reliability = hit + edge + MFE − invalidation.");
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
        IReadOnlyDictionary<CriterionType, CriterionAccuracySnapshot> rolling)
    {
        weights.TryGetValue(c.Type, out var w);
        rolling.TryGetValue(c.Type, out var roll);
        var action = CriterionReviewHelper.RecommendReliability(
            c.ReliabilityScore,
            c.EdgePercent,
            roll?.TotalCount ?? c.TotalCount);
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
            MapPhases(c.Phases));
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
