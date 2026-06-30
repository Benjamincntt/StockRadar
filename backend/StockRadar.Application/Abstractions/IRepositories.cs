using StockRadar.Domain.Entities;

namespace StockRadar.Application.Abstractions;

public interface IStockRepository
{
    Task<IReadOnlyList<Stock>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetActiveSymbolsAsync(CancellationToken cancellationToken = default);
    Task<Stock?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockSummaryRow>> GetSummariesBySymbolsAsync(
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken = default);
}

public sealed record StockSummaryRow(
    string Symbol,
    string Name,
    string Sector,
    bool SectorLocked,
    decimal LastChangePercent);

/// <summary>Đọc stock từ DB — dùng cho pipeline Job 2/3, không qua memory cache API.</summary>
public interface IJobStockRepository : IStockRepository;

public interface IAlertRepository
{
    Task<IReadOnlyList<Alert>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Alert>> GetForSessionDateAsync(
        DateOnly sessionDate,
        int take,
        CancellationToken cancellationToken = default);

    Task AddAsync(Alert alert, CancellationToken cancellationToken = default);
}

public interface IWatchlistRepository
{
    Task<IReadOnlyList<string>> GetSymbolsAsync(CancellationToken cancellationToken = default);
    Task AddAsync(string symbol, CancellationToken cancellationToken = default);
    Task RemoveAsync(string symbol, CancellationToken cancellationToken = default);
    Task<bool> ContainsAsync(string symbol, CancellationToken cancellationToken = default);
}

public interface IMarketIndexProvider
{
    Task<MarketIndex> GetCurrentAsync(CancellationToken cancellationToken = default);
}

/// <summary>Đọc VNINDEX từ DB — dùng cho pipeline Job 2/3, không qua memory cache API.</summary>
public interface IJobMarketIndexProvider : IMarketIndexProvider;
