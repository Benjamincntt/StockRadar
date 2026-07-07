namespace StockRadar.Application.Options;

public sealed class DarvasBoxOptions
{
    public decimal MaxBoxHeightPercent { get; set; } = 9m;
    public decimal ShadowTolerancePercent { get; set; } = 3m;
    public decimal VolDryUpRatio { get; set; } = 0.80m;
    public decimal TouchThresholdPercent { get; set; } = 1.5m;
    public int MinTopTouches { get; set; } = 2;
    public int MinBottomTouches { get; set; } = 2;
    public decimal MaxLast3AvgRangePercent { get; set; } = 3.5m;
    public decimal BreakoutMinPriceGainPercent { get; set; } = 2.5m;
    public decimal BreakoutMinVolumeMultiplier { get; set; } = 1.5m;
    public decimal BreakoutMaxUpperShadowRatio { get; set; } = 0.25m;
    public decimal BreakoutMaxBoxHeightPercent { get; set; } = 10m;
}
