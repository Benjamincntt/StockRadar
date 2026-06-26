using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

public interface IMarketSyncService
{
    Task<MarketSyncResultDto> ApplyAsync(MarketSyncRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetTrackedSymbolsAsync(CancellationToken cancellationToken = default);
}
