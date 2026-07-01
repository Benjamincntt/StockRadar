using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Enums;

namespace StockRadar.Application.Abstractions;

public interface IMarketService
{
    Task<MarketOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QuoteTickDto>> GetQuoteSnapshotAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SparklineDto>> GetSparklinesAsync(
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken = default);
    Task<PagedResult<SectorDto>> GetSectorsAsync(PaginationQuery query, CancellationToken cancellationToken = default);
    Task<OpportunitiesListDto> GetOpportunitiesAsync(PaginationQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetOpportunitySymbolsAsync(CancellationToken cancellationToken = default);
    Task<DailyAnalysisResultDto> RunOpportunityAnalysisAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<SignalDto>> GetSignalsAsync(PaginationQuery query, CancellationToken cancellationToken = default);
}

public interface IRadarService
{
    Task<PagedResult<RadarItemDto>> GetRadarAsync(RadarQuery query, CancellationToken cancellationToken = default);

    Task<RadarLiveSnapshotDto> GetLiveRadarAsync(
        RadarLiveQuery query,
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

public interface IAlertService
{
    Task<PagedResult<AlertDto>> GetAlertsAsync(AlertQuery query, CancellationToken cancellationToken = default);
}

public interface IIntradayMonitorStatusQuery
{
    IntradayMonitorStatusDto GetStatus();
}

public interface IWatchlistService
{
    Task<IReadOnlyList<WatchlistItemDto>> GetItemsAsync(CancellationToken cancellationToken = default);
    Task<bool> AddAsync(string symbol, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(string symbol, CancellationToken cancellationToken = default);
}

public interface ISectorCatalogService
{
    Task<IReadOnlyList<string>> GetCatalogAsync(CancellationToken cancellationToken = default);
    Task<StockSectorUpdateResultDto> UpdateStockSectorAsync(
        string symbol,
        UpdateStockSectorRequest request,
        CancellationToken cancellationToken = default);
}

public interface ISignalFormatter
{
    string FormatTitle(SignalType type, string symbol);
    string FormatAlertTitle(SignalType type, string symbol);
    string FormatDescription(SignalType type, string symbol, decimal volumeRatio);
    string GetLabelVi(SignalType type);
}
