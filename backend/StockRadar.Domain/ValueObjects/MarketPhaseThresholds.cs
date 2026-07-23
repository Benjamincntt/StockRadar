namespace StockRadar.Domain.ValueObjects;

/// <summary>Ngưỡng xác nhận Favorable (FTD + Higher Low). Độc lập MarketRegime sóng hồi.</summary>
public sealed record MarketPhaseThresholds(
    decimal FtdMinGainPercent = 1.2m,
    int FtdMinRallyDay = 4,
    int FtdMaxRallyDay = 7,
    int Ma20SlopeLookbackSessions = 3,
    int HigherLowLookbackSessions = 60,
    int HigherLowPivotRadius = 2,
    int RallyLookbackSessions = 20)
{
    public static MarketPhaseThresholds Default { get; } = new();
}
