namespace StockRadar.Domain.ValueObjects;

/// <summary>Tham số nhận diện hộp phẳng Darvas (Close-based, chống nhiễu râu nến VN).</summary>
public sealed record DarvasBoxSettings(
    decimal MaxBoxHeightPercent = 9m,
    decimal ShadowTolerancePercent = 3m,
    decimal VolDryUpRatio = 0.80m,
    decimal TouchThresholdPercent = 1.5m,
    int MinTopTouches = 2,
    int MinBottomTouches = 2,
    decimal MaxLast3AvgRangePercent = 3.5m,
    decimal BreakoutMinPriceGainPercent = 4m,
    decimal BreakoutMinVolumeMultiplier = 2m,
    decimal BreakoutMaxUpperShadowRatio = 0.25m,
    decimal BreakoutMaxBoxHeightPercent = 10m)
{
    public static DarvasBoxSettings Default { get; } = new();
}
