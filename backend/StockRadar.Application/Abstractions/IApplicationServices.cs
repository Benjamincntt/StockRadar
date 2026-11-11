using StockRadar.Application.Common;
using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

public interface IMarketService
{
    Task<MarketOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<VnIndexChartDto> GetVnIndexChartAsync(int sessions = 90, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuoteTickDto>> GetQuoteSnapshotAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SparklineDto>> GetSparklinesAsync(
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken = default);
}

public interface IStockService
{
    Task<StockDetailDto?> GetDetailAsync(string symbol, CancellationToken cancellationToken = default);
    Task<StockChartDto?> GetChartAsync(string symbol, string interval, CancellationToken cancellationToken = default);
}

public interface IStockLookupService
{
    Task<IReadOnlyList<StockSearchHitDto>> SearchAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default);
}

public interface IWatchlistService
{
    // Backward compat — thao tác trên danh sách mặc định của user hiện tại.
    Task<IReadOnlyList<WatchlistItemDto>> GetItemsAsync(CancellationToken cancellationToken = default);
    Task<bool> AddAsync(string symbol, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(string symbol, CancellationToken cancellationToken = default);

    // Multi-watchlist — api/v1/watchlists.

    /// <summary>Tất cả danh sách của user (mặc định + ngành + tùy chỉnh) — lazy seed lần đầu gọi.</summary>
    Task<IReadOnlyList<WatchlistDto>> GetWatchlistsAsync(CancellationToken cancellationToken = default);
    Task<WatchlistDto?> GetWatchlistAsync(int id, CancellationToken cancellationToken = default);
    Task<WatchlistDto> CreateWatchlistAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> RenameWatchlistAsync(int id, string newName, CancellationToken cancellationToken = default);
    Task<bool> DeleteWatchlistAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Items của một danh sách (enrich giá trị + điểm). Danh sách ngành: query Stocks theo ngành động.</summary>
    Task<IReadOnlyList<WatchlistItemDto>> GetWatchlistItemsAsync(
        int watchlistId,
        CancellationToken cancellationToken = default);
    Task<bool> AddItemAsync(int watchlistId, string symbol, CancellationToken cancellationToken = default);
    Task<bool> RemoveItemAsync(int watchlistId, string symbol, CancellationToken cancellationToken = default);
}

public interface ISectorCatalogService
{
    Task<IReadOnlyList<string>> GetCatalogAsync(CancellationToken cancellationToken = default);
    Task<StockSectorUpdateResultDto> UpdateStockSectorAsync(
        string symbol,
        UpdateStockSectorRequest request,
        CancellationToken cancellationToken = default);
}
