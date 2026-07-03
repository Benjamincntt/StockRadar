using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Options;

public sealed class PriceRunupFilterOptions
{
    public const string SectionName = "PriceRunupFilter";

    public int ConsolidationMinSessions { get; set; } = 10;
    public int MaxScanSessions { get; set; } = 90;
    public int MaxBaseWindowSessions { get; set; } = 45;
    public decimal MaxGainFromBasePercent { get; set; } = 10m;
    public int MinBaseQualityScore { get; set; } = 50;
    public int StrongBaseQualityScore { get; set; } = 80;
    public int IdealBaseMinSessions { get; set; } = 15;
    public int IdealBaseMaxSessions { get; set; } = 40;
    public decimal MinPriorImpulsePercent { get; set; } = 15m;
    public int PriorImpulseLookbackSessions { get; set; } = 30;

    public DarvasBoxOptions Darvas { get; set; } = new();

    public BasePriceFilterSettings ToSettings() => new(
        ConsolidationMinSessions,
        MaxScanSessions,
        MaxBaseWindowSessions,
        MaxGainFromBasePercent,
        MinBaseQualityScore,
        StrongBaseQualityScore,
        IdealBaseMinSessions,
        IdealBaseMaxSessions,
        MinPriorImpulsePercent,
        PriorImpulseLookbackSessions,
        new DarvasBoxSettings(
            Darvas.MaxBoxHeightPercent,
            Darvas.ShadowTolerancePercent,
            Darvas.VolDryUpRatio,
            Darvas.TouchThresholdPercent,
            Darvas.MinTopTouches,
            Darvas.MinBottomTouches,
            Darvas.MaxLast3AvgRangePercent,
            Darvas.BreakoutMinPriceGainPercent,
            Darvas.BreakoutMinVolumeMultiplier,
            Darvas.BreakoutMaxUpperShadowRatio,
            Darvas.BreakoutMaxBoxHeightPercent));
}
