namespace StockRadar.Infrastructure.Persistence.Entities;

/// <summary>Trạng thái Sóng ngành xuyên phiên (spec 007). Key = (Sector, TradingDate), idempotent.</summary>
public sealed class SectorWaveRegimeEntity
{
    public string Sector { get; set; } = "";
    public DateOnly TradingDate { get; set; }
    public bool IsActive { get; set; }
    public DateOnly ActivatedOn { get; set; }
    public int SessionsSinceActivation { get; set; }
    public int ConsecutiveLowVolumeSessions { get; set; }
    public DateOnly? FailedOn { get; set; }
}
