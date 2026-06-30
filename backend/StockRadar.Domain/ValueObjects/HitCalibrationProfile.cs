namespace StockRadar.Domain.ValueObjects;

public sealed record HitCalibrationBucket(
    string BucketId,
    int PredictedMin,
    int PredictedMax,
    int SampleCount,
    int GoodCount,
    decimal PredictedMidPercent,
    decimal ActualHitRatePercent,
    decimal CalibrationFactor);

/// <summary>Hiệu chỉnh P(hit) từ kết quả setup đã đo.</summary>
public sealed class HitCalibrationProfile
{
    public static HitCalibrationProfile Default { get; } = new([], 1m, 0);

    public HitCalibrationProfile(
        IReadOnlyList<HitCalibrationBucket> buckets,
        decimal globalFactor,
        int totalSamples)
    {
        Buckets = buckets;
        GlobalFactor = globalFactor;
        TotalSamples = totalSamples;
        _byId = buckets.ToDictionary(b => b.BucketId, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<HitCalibrationBucket> Buckets { get; }
    public decimal GlobalFactor { get; }
    public int TotalSamples { get; }
    public bool IsCalibrated => TotalSamples >= MinSamplesForGlobal;

    public const int MinSamplesPerBucket = 5;
    public const int MinSamplesForGlobal = 10;

    private readonly IReadOnlyDictionary<string, HitCalibrationBucket> _byId;

    public decimal Apply(decimal rawPercent)
    {
        if (rawPercent <= 0)
            return rawPercent;

        if (!IsCalibrated)
            return rawPercent;

        var bucket = ResolveBucket(rawPercent);
        var factor = bucket is not null && bucket.SampleCount >= MinSamplesPerBucket
            ? bucket.CalibrationFactor
            : GlobalFactor;

        return Math.Clamp(Math.Round(rawPercent * factor, 1), 5m, 92m);
    }

    private HitCalibrationBucket? ResolveBucket(decimal rawPercent)
    {
        foreach (var b in Buckets)
        {
            if (rawPercent >= b.PredictedMin && rawPercent <= b.PredictedMax)
                return b;
        }

        return _byId.Values.FirstOrDefault();
    }
}

public sealed record SetupPredictionSample(
    decimal PredictedHitPercent,
    bool IsGood);
