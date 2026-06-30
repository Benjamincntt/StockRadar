using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;

namespace StockRadar.Domain.ValueObjects;

public sealed record CriterionScore(
    CriterionType Type,
    int Score,
    PatternBias Bias,
    string Summary);

public sealed record CriterionForwardOutcome(
    decimal ForwardChangePercent,
    decimal MaxFavorablePercent,
    decimal MaxAdversePercent,
    bool InvalidatedBase,
    decimal RelativeStrengthForward,
    bool IsHit);

public sealed record CriterionScoreBucketStats(
    string BucketId,
    int HitCount,
    int TotalCount,
    decimal AccuracyPercent);

public sealed record CriterionPhaseStats(
    MarketWyckoffPhase Phase,
    int HitCount,
    int TotalCount,
    decimal AccuracyPercent);

public sealed record CriterionAccuracySnapshot(
    CriterionType Type,
    int HitCount,
    int TotalCount,
    decimal AccuracyPercent,
    decimal AvgScore = 0,
    decimal AvgMfePercent = 0,
    decimal AvgMaePercent = 0,
    decimal InvalidationRatePercent = 0,
    decimal BaselinePercent = 0,
    decimal EdgePercent = 0,
    decimal ReliabilityScore = 0,
    IReadOnlyList<CriterionScoreBucketStats>? Buckets = null,
    IReadOnlyList<CriterionPhaseStats>? Phases = null);

public sealed record CriterionWeight(
    CriterionType Type,
    decimal Weight,
    decimal Accuracy7d,
    int SampleCount7d,
    decimal Accuracy30d = 0,
    bool IsActive = true,
    CriterionReviewAction RecommendedAction = CriterionReviewAction.Keep,
    decimal Reliability7d = 0,
    decimal Edge7d = 0);

public sealed record CriterionGroupAccuracySnapshot(
    string GroupId,
    int HitCount,
    int TotalCount,
    decimal AccuracyPercent,
    decimal AvgScore,
    int CriterionCount,
    decimal ReliabilityScore = 0,
    decimal EdgePercent = 0);

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
    bool IsActive,
    decimal Edge7d = 0,
    decimal Reliability7d = 0,
    decimal AvgMfe7d = 0,
    decimal InvalidationRate7d = 0,
    IReadOnlyList<CriterionScoreBucketStats>? Buckets = null,
    IReadOnlyList<CriterionPhaseStats>? Phases = null);

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
    decimal ForwardChangePercent,
    bool MatchedOutcome,
    decimal MaxFavorablePercent = 0,
    decimal MaxAdversePercent = 0,
    bool InvalidatedBase = false,
    decimal RelativeStrengthForward = 0,
    string ScoreBucket = "",
    MarketWyckoffPhase MarketPhase = MarketWyckoffPhase.Neutral);

public sealed record StockCriterionScoreRecord(
    DateOnly AsOfDate,
    string Symbol,
    int CompositeScore,
    decimal ForwardChangePercent,
    IReadOnlyList<CriterionScore> Scores);
