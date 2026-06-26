using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.MarketData;

internal sealed class KbsChartBarProvider(KbsHistoryClient kbs) : IChartBarProvider
{
    public bool IsSupportedInterval(string interval) => KbsHistoryClient.IsSupported(interval);

    public async Task<IReadOnlyList<ChartBarDto>> FetchAsync(
        string symbol,
        string interval,
        CancellationToken cancellationToken = default)
    {
        var bars = await kbs.FetchAsync(symbol, interval, cancellationToken);
        return bars
            .Select(b => new ChartBarDto(
                b.Time.ToString("o"),
                b.Open,
                b.High,
                b.Low,
                b.Close,
                b.Volume))
            .ToList();
    }
}
