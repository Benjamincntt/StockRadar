namespace StockRadar.Application.DTOs;

/// <param name="SauDongCua">true khi yêu cầu đến từ Job 2 sau đóng cửa — cho phép ghi nến hôm nay ngoài khung KBS sync (spec 018 Q1/Q4).</param>
public sealed record MarketSyncRequest(
    MarketIndexSyncDto? Index,
    IReadOnlyList<StockQuoteSyncDto> Quotes,
    bool SauDongCua = false);

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
