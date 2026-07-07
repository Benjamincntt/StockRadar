namespace StockRadar.Infrastructure.Persistence.Entities;

public sealed class StockEntity
{
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string Sector { get; set; } = "";
    public bool SectorLocked { get; set; }
    public string HistoryJson { get; set; } = "[]";
    public decimal LastChangePercent { get; set; }
    public bool IsActive { get; set; }
    public string Exchange { get; set; } = "";
    public decimal AvgVolume30d { get; set; }
    public bool TradingRestricted { get; set; }
    public string? TradingStatus { get; set; }
    public DateOnly? FirstTradeDate { get; set; }
    public DateTime? UniverseUpdatedAt { get; set; }
}

public sealed class DailyAnalysisRunEntity
{
    public DateOnly ForTradingDate { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int StocksScored { get; set; }
    public int OpportunitiesSaved { get; set; }
    public bool UsedRelaxedFallback { get; set; }
}

public sealed class AlertEntity
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = "";
    public int Type { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int Category { get; set; }
    public decimal? VolumeRatio { get; set; }
    public decimal? RelativeStrength { get; set; }
    public string? SectorRank { get; set; }
}

public sealed class MarketIndexEntity
{
    public string Symbol { get; set; } = "VNINDEX";
    public decimal Price { get; set; }
    public decimal ChangePercent { get; set; }
    public int Score { get; set; }
    public int Trend { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string HistoryJson { get; set; } = "[]";
}

public sealed class UserEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsGuest { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class WatchlistItemEntity
{
    public Guid UserId { get; set; }
    public string Symbol { get; set; } = "";
    public DateTime AddedAt { get; set; }
}

public sealed class SectorDefinitionEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
