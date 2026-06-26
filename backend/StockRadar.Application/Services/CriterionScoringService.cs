using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Services;

public sealed class CriterionScoringService(
    ICriterionScoringRepository repo,
    ITechnicalIndicatorAnalyzer indicatorAnalyzer) : ICriterionScoringService
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

        var daily = await repo.GetDailyAccuracyAsync(asOf.Value, cancellationToken);
        var groups = await repo.GetGroupDailyAccuracyAsync(asOf.Value, cancellationToken);
        var weightDetails = await repo.GetWeightDetailsAsync(cancellationToken);
        var weightMap = weightDetails.ToDictionary(w => w.Type);

        var from7d = asOf.Value.AddDays(-7);
        var rolling = await repo.GetAccuracyRollingAsync(from7d, asOf.Value, cancellationToken);
        var rollingMap = rolling.ToDictionary(r => r.Type);

        var weekStart = CriterionReviewHelper.GetWeekStart(asOf.Value);
        var weeklyDb = await repo.GetWeeklyReviewsAsync(weekStart, cancellationToken);
        var weeklyGroupsDb = await repo.GetGroupWeeklyReviewsAsync(weekStart, cancellationToken);

        var criteria = daily
            .Select(c => ToAccuracyDto(c, weightMap, rollingMap))
            .OrderBy(c => c.Rank)
            .ThenByDescending(c => c.AccuracyPercent)
            .ToList();

        var groupDtos = weeklyGroupsDb.Count > 0
            ? weeklyGroupsDb.Select(ToGroupWeeklyDto).OrderByDescending(g => g.AccuracyPercent).ToList()
            : groups.Select(g => new CriterionGroupAccuracyDto(
                g.GroupId,
                g.HitCount,
                g.TotalCount,
                g.AccuracyPercent,
                g.AvgScore,
                g.CriterionCount,
                CriterionReviewAction.Keep.ToString(),
                0, 0, 0)).ToList();

        var weeklyReview = weeklyDb.Count > 0
            ? weeklyDb.Select(ToWeeklyDto).OrderBy(w => w.Rank).ToList()
            : criteria.Select(c => new WeeklyCriterionReviewDto(
                c.Id,
                c.Label,
                c.Group,
                c.Rank,
                c.HitCount,
                c.TotalCount,
                c.Accuracy7d,
                c.AvgScore,
                c.Weight,
                c.RecommendedAction,
                c.IsActive)).ToList();

        var topStocks = await BuildTopStocksAsync(asOf.Value, cancellationToken);

        return new CriteriaSummaryDto(
            asOf,
            weekStart,
            null,
            criteria,
            groupDtos,
            weeklyReview,
            topStocks,
            $"T-1 {asOf.Value:dd/MM/yyyy} · Tuần từ {weekStart:dd/MM/yyyy} — đối chiếu biến động phiên sau.");
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
            (w?.RecommendedAction ?? CriterionReviewAction.Keep).ToString(),
            w?.IsActive ?? true);
    }

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
        g.RemoveCount);

    private static WeeklyCriterionReviewDto ToWeeklyDto(WeeklyCriterionReviewSnapshot w) => new(
        w.Type.ToString(),
        w.Label,
        w.GroupId,
        w.Rank,
        w.HitCount7d,
        w.TotalCount7d,
        w.Accuracy7d,
        w.AvgScore7d,
        w.Weight,
        w.RecommendedAction.ToString(),
        w.IsActive);

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
