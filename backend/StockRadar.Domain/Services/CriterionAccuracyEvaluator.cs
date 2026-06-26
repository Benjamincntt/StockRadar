using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

public interface ICriterionAccuracyEvaluator
{
    bool MatchesOutcome(PatternBias bias, int score, decimal nextDayChangePercent);

    int ComputeCompositeScore(
        IReadOnlyList<CriterionScore> scores,
        IReadOnlyDictionary<CriterionType, decimal> weights);
}

public sealed class CriterionAccuracyEvaluator : ICriterionAccuracyEvaluator
{
    private const decimal NeutralThreshold = 0.3m;
    private const int MinScoreForSignal = 55;

    public bool MatchesOutcome(PatternBias bias, int score, decimal nextDayChangePercent)
    {
        if (score < MinScoreForSignal)
            return Math.Abs(nextDayChangePercent) <= NeutralThreshold;

        return bias switch
        {
            PatternBias.Bullish => nextDayChangePercent > NeutralThreshold,
            PatternBias.Bearish => nextDayChangePercent < -NeutralThreshold,
            _ => Math.Abs(nextDayChangePercent) <= NeutralThreshold,
        };
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
