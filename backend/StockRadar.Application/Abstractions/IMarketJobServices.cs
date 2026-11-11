using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

public interface IDailySessionSyncService
{
    Task<DailySessionSyncResultDto> RunAsync(CancellationToken cancellationToken = default);
}

public interface IKbsMarketSyncService
{
    Task RunAsync(CancellationToken cancellationToken = default);
}

public interface IUniverseRescreenService
{
    Task<UniverseRescreenResultDto> RunAsync(CancellationToken cancellationToken = default);
}
