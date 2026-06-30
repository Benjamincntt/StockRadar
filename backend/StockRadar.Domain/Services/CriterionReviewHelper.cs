using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

public static class CriterionReviewHelper
{
    public const int MinSamplesForReview = 30;
    public const decimal RemoveBelowReliability = 42m;
    public const decimal WatchBelowReliability = 50m;
    public const decimal RemoveBelowEdge = 3m;
    public const decimal GroupRemoveBelowReliability = 45m;

    public static CriterionReviewAction Recommend(decimal accuracy7d, int sampleCount) =>
        RecommendReliability(accuracy7d, 0m, sampleCount);

    public static CriterionReviewAction RecommendReliability(
        decimal reliability7d,
        decimal edge7d,
        int sampleCount)
    {
        if (sampleCount < MinSamplesForReview)
            return CriterionReviewAction.Keep;

        // Dữ liệu cũ / chưa tính trend metrics — không gợi ý loại hàng loạt.
        if (reliability7d <= 0 && edge7d == 0)
            return CriterionReviewAction.Keep;

        if (reliability7d < RemoveBelowReliability && edge7d < RemoveBelowEdge)
            return CriterionReviewAction.Remove;
        if (reliability7d < WatchBelowReliability)
            return CriterionReviewAction.Watch;
        return CriterionReviewAction.Keep;
    }

    public static CriterionReviewAction RecommendGroup(decimal reliability7d, int sampleCount)
    {
        if (sampleCount < MinSamplesForReview * 3)
            return CriterionReviewAction.Keep;
        if (reliability7d < GroupRemoveBelowReliability)
            return CriterionReviewAction.Remove;
        if (reliability7d < WatchBelowReliability)
            return CriterionReviewAction.Watch;
        return CriterionReviewAction.Keep;
    }

    public static decimal ComputeWeight(decimal reliability7d, int samples, bool isActive)
    {
        if (!isActive)
            return 0.25m;
        if (samples < MinSamplesForReview)
            return 1m;
        return Math.Round(0.5m + reliability7d / 100m * 1.5m, 2);
    }

    public static CriterionWeight ApplyFalsePositivePenalty(CriterionWeight weight, decimal penalty)
    {
        if (penalty <= 0)
            return weight;

        var newWeight = Math.Round(Math.Max(0.25m, weight.Weight * (1m - penalty)), 2);
        var action = weight.RecommendedAction;
        if (penalty >= 0.45m)
            action = CriterionReviewAction.Remove;
        else if (penalty >= 0.22m && action == CriterionReviewAction.Keep)
            action = CriterionReviewAction.Watch;

        return weight with
        {
            Weight = newWeight,
            RecommendedAction = action,
            IsActive = action != CriterionReviewAction.Remove,
        };
    }

    public static DateOnly GetWeekStart(DateOnly date)
    {
        var dow = (int)date.DayOfWeek;
        var offset = dow == 0 ? 6 : dow - 1;
        return date.AddDays(-offset);
    }
}
