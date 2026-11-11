using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Mapping;
using StockRadar.Application.Services;

namespace StockRadar.Application.Services;

public sealed class MarketService(
    IStockRepository stocks,
    IMarketIndexProvider marketIndex,
    IQuoteTickCache quoteCache,
    IChartBarProvider chartBars) : IMarketService
{
    public async Task<MarketOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var index = await marketIndex.GetCurrentAsync(cancellationToken);
        return DtoMapper.ToDto(index);
    }

    public async Task<VnIndexChartDto> GetVnIndexChartAsync(
        int sessions = 90,
        CancellationToken cancellationToken = default)
    {
        _ = sessions; // giữ query param tương thích client cũ
        var index = await marketIndex.GetCurrentAsync(cancellationToken);
        var breadth = await stocks.GetBreadthStatsAsync(cancellationToken);
        var bars = index.Bars;

        decimal changePoints = 0m;
        if (bars.Count >= 2 && bars[^2].Close > 0)
            changePoints = Math.Round(index.Price - bars[^2].Close, 2);
        else if (index.Price != 0)
            changePoints = Math.Round(index.Price * index.ChangePercent / 100m, 2);

        var indexVolume = bars.Count > 0 ? bars[^1].Volume : breadth.TotalVolume;
        var kl = indexVolume > 0 ? indexVolume : breadth.TotalVolume;
        var gtTy = Math.Round(breadth.TurnoverCloseTimesVol / 1_000_000m, 1);

        var asOf = bars.Count > 0
            ? DateTime.SpecifyKind(bars[^1].Date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
            : DateTime.UtcNow;

        return new VnIndexChartDto(
            index.Symbol,
            index.Price,
            index.ChangePercent,
            changePoints,
            kl,
            gtTy,
            breadth.Advancing,
            breadth.Unchanged,
            breadth.Declining,
            asOf);
    }

    public async Task<IReadOnlyList<QuoteTickDto>> GetQuoteSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var cached = quoteCache.GetQuotes();
        if (cached.Count > 0)
            return cached;

        var all = await stocks.GetAllAsync(cancellationToken);
        var now = DateTime.UtcNow;

        return all
            .Select(stock =>
            {
                var last = stock.History.LastOrDefault();
                if (last is null)
                    return null;

                var change = stock.LastChangePercent != 0
                    ? stock.LastChangePercent
                    : ChangePercentFromHistory(stock.History);

                return new QuoteTickDto(
                    stock.Symbol,
                    last.Close,
                    change,
                    last.Volume,
                    now);
            })
            .Where(t => t is not null)
            .Cast<QuoteTickDto>()
            .OrderBy(t => t.Symbol)
            .ToList();
    }

    private static decimal ChangePercentFromHistory(IReadOnlyList<StockRadar.Domain.Entities.OhlcvBar> history)
    {
        if (history.Count >= 2 && history[^2].Close > 0)
            return Math.Round((history[^1].Close - history[^2].Close) / history[^2].Close * 100m, 2);

        if (history.Count == 1 && history[0].Open > 0)
            return Math.Round((history[0].Close - history[0].Open) / history[0].Open * 100m, 2);

        return 0m;
    }

    public async Task<IReadOnlyList<SparklineDto>> GetSparklinesAsync(
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken = default)
    {
        var unique = symbols
            .Select(s => s.Trim().ToUpperInvariant())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToList();

        if (unique.Count == 0)
            return [];

        var results = await Task.WhenAll(unique.Select(sym => BuildSparklineAsync(sym, cancellationToken)));
        return results.Where(r => r is not null).Cast<SparklineDto>().ToList();
    }

    private async Task<SparklineDto?> BuildSparklineAsync(string symbol, CancellationToken cancellationToken)
    {
        var bars = await chartBars.FetchAsync(symbol, "5m", cancellationToken);
        if (bars.Count < 2)
            bars = await chartBars.FetchAsync(symbol, "1D", cancellationToken);

        if (bars.Count < 2)
            return null;

        var prices = bars.Select(b => b.Close).ToList();
        var reference = bars[0].Open > 0 ? bars[0].Open : prices[0];
        return new SparklineDto(symbol, prices, reference);
    }
}
