using StockRadar.Application.DTOs;
using StockRadar.Domain.Services.ReversalBounce;

namespace StockRadar.Application.Abstractions;

/// <summary>Lưu/đọc snapshot breadth + regime theo phiên (idempotent theo TradingDate).</summary>
public interface IMarketBreadthSnapshotRepository
{
    Task UpsertAsync(MarketBreadthSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<MarketBreadthSnapshot?> GetForDateAsync(DateOnly tradingDate, CancellationToken cancellationToken = default);

    /// <summary>Snapshot phiên gần nhất trước <paramref name="beforeDate"/> (cho hysteresis).</summary>
    Task<MarketBreadthSnapshot?> GetPreviousAsync(DateOnly beforeDate, CancellationToken cancellationToken = default);

    Task<MarketBreadthSnapshot?> GetLatestAsync(CancellationToken cancellationToken = default);
}

/// <summary>Query đọc cho tab Sóng hồi (regime + danh sách ứng viên counter-trend).</summary>
public interface IReversalBounceQueryService
{
    Task<MarketRegimeDto> GetMarketRegimeAsync(CancellationToken cancellationToken = default);

    Task<ReversalBounceListDto> GetCandidatesAsync(
        DateOnly? date,
        string? stage,
        bool? actionableOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ReversalBounceDetailDto?> GetBySymbolAsync(
        string symbol,
        int lookback,
        CancellationToken cancellationToken = default);
}
