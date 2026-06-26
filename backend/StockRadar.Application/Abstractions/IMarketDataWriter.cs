using StockRadar.Application.DTOs;
using StockRadar.Domain.Entities;

namespace StockRadar.Application.Abstractions;

public interface IMarketDataWriter
{
    Task<int> UpsertQuotesAsync(IReadOnlyList<StockQuoteSyncDto> quotes, CancellationToken cancellationToken = default);
    Task UpsertIndexAsync(MarketIndexSyncDto index, CancellationToken cancellationToken = default);

    /// <summary>Job 1: ghi/ghép toàn bộ lịch sử ngày (2000 → T-1), không cắt.</summary>
    Task<int> UpsertStockHistoryAsync(
        string symbol,
        string? name,
        string? sector,
        IReadOnlyList<OhlcvBar> bars,
        CancellationToken cancellationToken = default);

    Task UpsertUniverseStockAsync(
        UniverseStockUpsert upsert,
        CancellationToken cancellationToken = default);

    Task MarkUniverseInactiveAsync(
        string symbol,
        string reason,
        DateTime updatedAt,
        CancellationToken cancellationToken = default);

    Task SetTradingRestrictedAsync(
        string symbol,
        bool restricted,
        string? status,
        CancellationToken cancellationToken = default);

    Task DeactivateUniverseExceptAsync(
        IReadOnlyCollection<string> activeSymbols,
        DateTime updatedAt,
        CancellationToken cancellationToken = default);
}

public sealed record UniverseStockUpsert(
    string Symbol,
    string? Name,
    string? Sector,
    string Exchange,
    IReadOnlyList<OhlcvBar> Bars,
    bool IsActive,
    bool TradingRestricted,
    string? TradingStatus,
    decimal AvgVolume30d,
    DateOnly? FirstTradeDate,
    DateTime UniverseUpdatedAt);
