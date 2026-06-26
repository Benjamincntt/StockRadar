namespace StockRadar.Application.DTOs;

public sealed record MarketSyncRequest(
    MarketIndexSyncDto? Index,
    IReadOnlyList<StockQuoteSyncDto> Quotes);

public sealed record MarketIndexSyncDto(
    string Symbol,
    decimal Price,
    decimal ChangePercent);

public sealed record StockQuoteSyncDto(
    string Symbol,
    string? Name,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume,
    decimal ChangePercent,
    string? Sector = null);

public sealed record MarketSyncResultDto(
    int StocksUpdated,
    bool IndexUpdated,
    DateTime SyncedAt);
