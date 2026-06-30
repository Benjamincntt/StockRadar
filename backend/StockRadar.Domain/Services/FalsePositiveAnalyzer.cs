using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

public static class FalsePositiveAnalyzer
{
    public static FalsePositiveMiningResult Analyze(IReadOnlyList<SetupOutcomeBreakdownSample> samples)
    {
        var falsePositives = samples.Where(s => s.IsFalsePositive && s.Breakdown.Count > 0).ToList();
        var good = samples.Where(s => s.IsGood && s.Breakdown.Count > 0).ToList();

        if (falsePositives.Count < FalsePositiveThresholds.MinFalsePositiveSetups
            || good.Count < FalsePositiveThresholds.MinGoodSetups)
        {
            return new FalsePositiveMiningResult(falsePositives.Count, good.Count, []);
        }

        var penalties = new List<FalsePositiveCriterionPenalty>();
        foreach (var (criterionType, componentId) in AdaptiveScoringProfile.CriterionToComponent)
        {
            var fpNorms = falsePositives
                .Select(s => NormScore(s.Breakdown, componentId))
                .Where(n => n > 0)
                .ToList();
            if (fpNorms.Count < 2)
                continue;

            var goodNorms = good.Select(s => NormScore(s.Breakdown, componentId)).ToList();
            var fpAvg = (decimal)fpNorms.Average();
            var goodAvg = (decimal)goodNorms.Average();
            var gap = fpAvg - goodAvg;

            if (fpAvg < FalsePositiveThresholds.MinComponentNormOnFp
                || gap < FalsePositiveThresholds.MinDeceptionGap)
                continue;

            var deception = Math.Round(Math.Min(0.55m, gap * 1.8m), 3);
            penalties.Add(new FalsePositiveCriterionPenalty(
                componentId,
                criterionType,
                LabelFor(componentId),
                fpNorms.Count,
                Math.Round(fpAvg, 3),
                Math.Round(goodAvg, 3),
                deception,
                deception));
        }

        return new FalsePositiveMiningResult(
            falsePositives.Count,
            good.Count,
            penalties.OrderByDescending(p => p.DeceptionScore).ToList());
    }

    private static double NormScore(IReadOnlyList<BuyScoreComponent> breakdown, string componentId)
    {
        var item = breakdown.FirstOrDefault(c =>
            string.Equals(c.Id, componentId, StringComparison.OrdinalIgnoreCase));
        if (item is null || item.MaxPoints <= 0)
            return 0;

        return (double)item.Points / item.MaxPoints;
    }

    private static string LabelFor(string componentId) => componentId switch
    {
        "market" => "Thị trường",
        "sector" => "Ngành",
        "rs" => "RS 5 phiên",
        "base" => "Nền giá",
        "breakout" => "Breakout",
        "shakeout" => "Shakeout",
        "volume" => "Volume spike",
        "wyckoff" => "Pha tăng giá",
        "trend" => "MA stack",
        _ => componentId,
    };
}
