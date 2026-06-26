namespace StockRadar.Domain.Entities;

public sealed record OhlcvBar(
    DateOnly Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);

public sealed record Stock(
    string Symbol,
    string Name,
    string Sector,
    IReadOnlyList<OhlcvBar> History,
    decimal LastChangePercent = 0,
    bool IsActive = true,
    string Exchange = "",
    bool TradingRestricted = false,
    bool SectorLocked = false)
{
    public decimal LatestPrice => History.Count > 0 ? History[^1].Close : 0;
}

public sealed record MarketIndex(
    string Symbol,
    decimal Price,
    decimal ChangePercent,
    int Score,
    Enums.MarketTrend Trend,
    decimal ChangePercent5d = 0)
{
    public decimal IndexChange5d => ChangePercent5d != 0 ? ChangePercent5d : ChangePercent;
}

public sealed record Alert(
    Guid Id,
    string Symbol,
    Enums.SignalType Type,
    string Title,
    string Message,
    DateTime CreatedAt,
    Enums.AlertCategory Category,
    decimal? VolumeRatio,
    decimal? RelativeStrength,
    string? SectorRank);
