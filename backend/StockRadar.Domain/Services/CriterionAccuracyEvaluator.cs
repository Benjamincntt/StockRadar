using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

public interface ICriterionAccuracyEvaluator
{
    bool ShouldEvaluate(int score, PatternBias bias);

    bool MatchesOutcome(PatternBias bias, int score, CriterionForwardOutcome outcome);

    decimal ComputeReliabilityScore(
        decimal hitRatePercent,
        decimal edgePercent,
        decimal avgMfePercent,
        decimal invalidationRatePercent);

    int ComputeCompositeScore(
        IReadOnlyList<CriterionScore> scores,
        IReadOnlyDictionary<CriterionType, decimal> weights);
}

public sealed class CriterionAccuracyEvaluator(CriterionAccuracySettings settings) : ICriterionAccuracyEvaluator
{
    public bool ShouldEvaluate(int score, PatternBias bias) =>
        score >= settings.MinScoreForEvaluation && bias != PatternBias.Neutral;

    public bool MatchesOutcome(PatternBias bias, int score, CriterionForwardOutcome outcome) =>
        ShouldEvaluate(score, bias) && outcome.IsHit;

    public decimal ComputeReliabilityScore(
        decimal hitRatePercent,
        decimal edgePercent,
        decimal avgMfePercent,
        decimal invalidationRatePercent)
    {
        var edgeNorm = Math.Clamp((edgePercent + 10m) * 5m, 0m, 100m);
        var mfeNorm = Math.Clamp(avgMfePercent / 5m * 100m, 0m, 100m);
        var intactNorm = Math.Clamp(100m - invalidationRatePercent, 0m, 100m);

        return Math.Round(
            0.4m * hitRatePercent +
            0.3m * edgeNorm +
            0.2m * mfeNorm +
            0.1m * intactNorm,
            1);
    }

    public int ComputeCompositeScore(
        IReadOnlyList<CriterionScore> scores,
        IReadOnlyDictionary<CriterionType, decimal> weights)
    {
        if (scores.Count == 0)
            return 0;

        decimal weighted = 0;
        decimal totalWeight = 0;
        foreach (var s in scores)
        {
            var w = weights.GetValueOrDefault(s.Type, 1m);
            weighted += s.Score * w;
            totalWeight += w;
        }

        if (totalWeight <= 0)
            return (int)Math.Round(scores.Average(x => x.Score));

        return (int)Math.Clamp(Math.Round(weighted / totalWeight), 0, 100);
    }
}
