using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

public static class HitCalibrationBuilder
{
    private static readonly (string Id, int Min, int Max, int Mid)[] BucketDefs =
    [
        ("50-59", 50, 59, 55),
        ("60-69", 60, 69, 65),
        ("70-79", 70, 79, 75),
        ("80-92", 80, 92, 85),
    ];

    public static HitCalibrationProfile Build(IReadOnlyList<SetupPredictionSample> samples)
    {
        if (samples.Count == 0)
            return HitCalibrationProfile.Default;

        var buckets = new List<HitCalibrationBucket>();
        foreach (var def in BucketDefs)
        {
            var inBucket = samples
                .Where(s => s.PredictedHitPercent >= def.Min && s.PredictedHitPercent <= def.Max)
                .ToList();
            if (inBucket.Count == 0)
                continue;

            var good = inBucket.Count(s => s.IsGood);
            var actual = Math.Round((decimal)good / inBucket.Count * 100m, 1);
            var factor = ComputeFactor(actual, def.Mid);
            buckets.Add(new HitCalibrationBucket(
                def.Id,
                def.Min,
                def.Max,
                inBucket.Count,
                good,
                def.Mid,
                actual,
                factor));
        }

        var totalGood = samples.Count(s => s.IsGood);
        var globalActual = Math.Round((decimal)totalGood / samples.Count * 100m, 1);
        var avgPredicted = samples.Average(s => (double)s.PredictedHitPercent);
        var globalFactor = ComputeFactor(globalActual, (decimal)avgPredicted);

        return new HitCalibrationProfile(buckets, globalFactor, samples.Count);
    }

    private static decimal ComputeFactor(decimal actualPercent, decimal predictedMid)
    {
        if (predictedMid <= 0)
            return 1m;

        var factor = actualPercent / predictedMid;
        return Math.Round(Math.Clamp(factor, 0.7m, 1.35m), 3);
    }

    public static decimal ComputePredictionBias(IReadOnlyList<SetupPredictionSample> samples)
    {
        if (samples.Count == 0)
            return 0m;

        var avgPredicted = (decimal)samples.Average(s => (double)s.PredictedHitPercent);
        var actual = (decimal)samples.Count(s => s.IsGood) / samples.Count * 100m;
        return Math.Round(avgPredicted - actual, 1);
    }
}
