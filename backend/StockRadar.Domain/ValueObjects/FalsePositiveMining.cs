using System.Text.Json;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.ValueObjects;

public sealed record SetupOutcomeBreakdownSample(
    decimal PredictedHitPercent,
    int OpportunityScore,
    string OutcomeBucket,
    IReadOnlyList<BuyScoreComponent> Breakdown)
{
    public bool IsFalsePositive =>
        string.Equals(OutcomeBucket, "Failed", StringComparison.OrdinalIgnoreCase)
        && PredictedHitPercent >= FalsePositiveThresholds.MinPredictedHitPercent
        && OpportunityScore >= FalsePositiveThresholds.MinOpportunityScore;

    public bool IsGood =>
        string.Equals(OutcomeBucket, "Good", StringComparison.OrdinalIgnoreCase);
}

public static class FalsePositiveThresholds
{
    public const decimal MinPredictedHitPercent = 55m;
    public const int MinOpportunityScore = 60;
    public const int MinFalsePositiveSetups = 3;
    public const int MinGoodSetups = 3;
    public const decimal MinComponentNormOnFp = 0.5m;
    public const decimal MinDeceptionGap = 0.12m;
}

public sealed record FalsePositiveCriterionPenalty(
    string ComponentId,
    CriterionType CriterionType,
    string Label,
    int FalsePositiveHits,
    decimal FalsePositiveAvgNorm,
    decimal GoodAvgNorm,
    decimal DeceptionScore,
    decimal WeightPenalty);

public sealed record FalsePositiveMiningResult(
    int FalsePositiveSetups,
    int GoodSetups,
    IReadOnlyList<FalsePositiveCriterionPenalty> Penalties)
{
    public bool HasActionablePenalties => Penalties.Count > 0;
}
