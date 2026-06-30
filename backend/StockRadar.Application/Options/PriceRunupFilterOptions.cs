using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Options;

public sealed class PriceRunupFilterOptions
{
    public const string SectionName = "PriceRunupFilter";

    public int ConsolidationMinSessions { get; set; } = 12;
    public int MaxScanSessions { get; set; } = 60;
    public int MaxBaseWindowSessions { get; set; } = 45;
    public decimal MaxGainFromBasePercent { get; set; } = 10m;
    public int MinBaseQualityScore { get; set; } = 60;
    public int StrongBaseQualityScore { get; set; } = 80;
    public int IdealBaseMinSessions { get; set; } = 15;
    public int IdealBaseMaxSessions { get; set; } = 40;
    public decimal MinPriorImpulsePercent { get; set; } = 15m;
    public int PriorImpulseLookbackSessions { get; set; } = 30;

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
        PriorImpulseLookbackSessions);
}
