namespace StockRadar.Infrastructure.Persistence.Entities;

public sealed class DailyOpportunityEntity
{
    public DateOnly ForTradingDate { get; set; }
    public int Rank { get; set; }
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string Sector { get; set; } = "";
    public int Score { get; set; }
    public decimal Price { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal VolumeRatio { get; set; }
    public DateTime GeneratedAt { get; set; }
}
