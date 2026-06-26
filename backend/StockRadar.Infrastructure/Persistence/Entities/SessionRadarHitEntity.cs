namespace StockRadar.Infrastructure.Persistence.Entities;

public sealed class SessionRadarHitEntity
{
    public DateOnly SessionDate { get; set; }
    public string Exchange { get; set; } = "HOSE";
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string Sector { get; set; } = "";
    public decimal Price { get; set; }
    public decimal ChangePercent { get; set; }
    public long SessionVolume { get; set; }
    public decimal VolumeRatio { get; set; }
    public decimal RelativeStrength { get; set; }
    public string SignalsJson { get; set; } = "[]";
    public DateTime ScannedAt { get; set; }
}
