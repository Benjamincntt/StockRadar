using StockRadar.Domain.Enums;

namespace StockRadar.Domain.Services;

public static class CriterionReviewHelper
{
    public const int MinSamplesForReview = 50;
    public const decimal RemoveBelowAccuracy = 42m;
    public const decimal WatchBelowAccuracy = 50m;
    public const decimal GroupRemoveBelowAccuracy = 45m;

    public static CriterionReviewAction Recommend(decimal accuracy7d, int sampleCount)
    {
        if (sampleCount < MinSamplesForReview)
            return CriterionReviewAction.Keep;
        if (accuracy7d < RemoveBelowAccuracy)
            return CriterionReviewAction.Remove;
        if (accuracy7d < WatchBelowAccuracy)
            return CriterionReviewAction.Watch;
        return CriterionReviewAction.Keep;
    }

    public static CriterionReviewAction RecommendGroup(decimal accuracy7d, int sampleCount)
    {
        if (sampleCount < MinSamplesForReview * 3)
            return CriterionReviewAction.Keep;
        if (accuracy7d < GroupRemoveBelowAccuracy)
            return CriterionReviewAction.Remove;
        if (accuracy7d < WatchBelowAccuracy)
            return CriterionReviewAction.Watch;
        return CriterionReviewAction.Keep;
    }

    public static decimal ComputeWeight(decimal accuracy7d, int samples, bool isActive)
    {
        if (!isActive)
            return 0.25m;
        if (samples < MinSamplesForReview)
            return 1m;
        return Math.Round(0.5m + accuracy7d / 100m * 1.5m, 2);
    }

    public static DateOnly GetWeekStart(DateOnly date)
    {
        var dow = (int)date.DayOfWeek;
        var offset = dow == 0 ? 6 : dow - 1;
        return date.AddDays(-offset);
    }
}
