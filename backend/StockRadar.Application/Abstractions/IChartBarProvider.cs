using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

public interface IChartBarProvider
{
    bool IsSupportedInterval(string interval);
    Task<IReadOnlyList<ChartBarDto>> FetchAsync(
        string symbol,
        string interval,
        CancellationToken cancellationToken = default);
}
