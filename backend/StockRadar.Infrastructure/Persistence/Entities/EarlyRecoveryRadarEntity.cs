namespace StockRadar.Infrastructure.Persistence.Entities;

public sealed class EarlyRecoveryRadarEntity
{
    public DateOnly ForTradingDate { get; set; }
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string Sector { get; set; } = "";
    public decimal Price { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal VolumeRatio { get; set; }
    public decimal Rs5 { get; set; }
    public decimal RsPercentile { get; set; }
    public string MarketPhase { get; set; } = "Neutral";
    public string Reason { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
}
