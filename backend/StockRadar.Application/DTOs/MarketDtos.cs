using StockRadar.Domain.Enums;

namespace StockRadar.Application.DTOs;

public record MarketOverviewDto(
    string IndexSymbol,
    decimal IndexPrice,
    decimal IndexChangePercent,
    int MarketScore,
    MarketTrend Trend);

public record OhlcvBarDto(
    string Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);

public record ChartBarDto(
    string Time,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);

public record StockChartDto(
    string Symbol,
    string Interval,
    IReadOnlyList<ChartBarDto> Bars);

/// <summary>Tổng quan VNINDEX trên Home (không chart).</summary>
public record VnIndexChartDto(
    string Symbol,
    decimal Price,
    decimal ChangePercent,
    decimal ChangePoints,
    long Volume,
    decimal TurnoverBillionVnd,
    int Advancing,
    int Unchanged,
    int Declining,
    DateTime AsOfUtc,
    string ExchangeLabel = "Sàn Chứng khoán TP.HCM");

/// <summary>Chi tiết mã — biểu đồ ngày (history) + trạng thái universe.</summary>
public record StockDetailDto(
    string Symbol,
    string Name,
    string Sector,
    decimal Price,
    decimal ChangePercent,
    IReadOnlyList<OhlcvBarDto> History,
    /// <summary>false = mã đã bị loại khỏi universe (Job 2 không còn tự động cập nhật giá).</summary>
    bool IsUniverseActive = true,
    string? UniverseStatusReason = null,
    DateTime? UniverseUpdatedAt = null);

public record WatchlistItemDto(
    string Symbol,
    string Name,
    string Sector,
    decimal ChangePercent,
    bool SectorLocked,
    string? TrangThaiV3 = null);

public record SectorCatalogItemDto(string Name);

public record UpdateStockSectorRequest(string Sector);

public record StockSectorUpdateResultDto(string Symbol, string Sector, bool SectorLocked);

public record CreateWatchlistItemRequest(string Symbol);

public record StockSearchHitDto(string Symbol, string Name);
