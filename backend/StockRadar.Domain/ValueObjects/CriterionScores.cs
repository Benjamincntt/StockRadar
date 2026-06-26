using StockRadar.Domain.Enums;

namespace StockRadar.Domain.ValueObjects;

public sealed record CriterionScore(
    CriterionType Type,
    int Score,
    PatternBias Bias,
    string Summary);

public sealed record CriterionAccuracySnapshot(
    CriterionType Type,
    int HitCount,
    int TotalCount,
    decimal AccuracyPercent,
    decimal AvgScore = 0);

public sealed record CriterionWeight(
    CriterionType Type,
    decimal Weight,
    decimal Accuracy7d,
    int SampleCount7d,
    decimal Accuracy30d = 0,
    bool IsActive = true,
    CriterionReviewAction RecommendedAction = CriterionReviewAction.Keep);

public sealed record CriterionGroupAccuracySnapshot(
    string GroupId,
    int HitCount,
    int TotalCount,
    decimal AccuracyPercent,
    decimal AvgScore,
    int CriterionCount);

public sealed record WeeklyCriterionReviewSnapshot(
    CriterionType Type,
    string GroupId,
    string Label,
    int Rank,
    int HitCount7d,
    int TotalCount7d,
    decimal Accuracy7d,
    decimal AvgScore7d,
    decimal Weight,
    CriterionReviewAction RecommendedAction,
    bool IsActive);

public sealed record CriterionGroupWeeklySnapshot(
    string GroupId,
    int HitCount,
    int TotalCount,
    decimal AccuracyPercent,
    decimal AvgScore,
    int KeepCount,
    int WatchCount,
    int RemoveCount,
    CriterionReviewAction RecommendedAction);

public sealed record StockCriterionDetailRecord(
    DateOnly AsOfDate,
    string Symbol,
    CriterionType Type,
    string GroupId,
    int Rank,
    int Score,
    PatternBias Bias,
    string Summary,
    decimal NextDayChangePercent,
    bool MatchedOutcome);
