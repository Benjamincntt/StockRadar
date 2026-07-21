namespace StockRadar.Infrastructure.Persistence.Entities;

/// <summary>Snapshot breadth + regime cấp thị trường theo phiên. Key = TradingDate (idempotent).</summary>
public sealed class MarketBreadthSnapshotEntity
{
    public DateOnly TradingDate { get; set; }
    public int UniverseCount { get; set; }
    public decimal PctAboveMa20 { get; set; }
    public decimal PctAboveMa50 { get; set; }
    public decimal PctNewLow20 { get; set; }
    public decimal PctUp { get; set; }
    public decimal PctDown { get; set; }
    public int FloorCount { get; set; }
    public int CeilingCount { get; set; }
    public decimal MedianReturnPercent { get; set; }
    public decimal MedianTurnover { get; set; }
    public decimal VnIndexDrawdownPercent { get; set; }
    public decimal VnIndexDistanceToMa20Percent { get; set; }
    public bool VnIndexAboveMa20 { get; set; }
    public bool VnIndexReclaimedMa20 { get; set; }
    public string Regime { get; set; } = "Normal";
    public int ImproveStreak { get; set; }
    public string Version { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
}
