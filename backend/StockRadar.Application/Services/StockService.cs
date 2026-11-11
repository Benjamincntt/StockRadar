using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Mapping;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

public sealed class StockService(
    IJobStockRepository jobStocks,
    IChartBarProvider chartBars) : IStockService
{
    private const int MaxHistoryBarsInDetail = 250;

    public async Task<StockDetailDto?> GetDetailAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var match = await jobStocks.GetBySymbolAsync(symbol, cancellationToken);
        if (match is null)
            return null;

        var historyDto = match.History
            .Where(b => TradingSessionMath.IsTradingDay(b.Date))
            .Skip(Math.Max(0, match.History.Count(b => TradingSessionMath.IsTradingDay(b.Date)) - MaxHistoryBarsInDetail))
            .Select(DtoMapper.ToDto)
            .ToList();

        var changePercent = match.LastChangePercent != 0
            ? match.LastChangePercent
            : ChangePercentFromHistory(match.History);

        return new StockDetailDto(
            match.Symbol,
            match.Name,
            match.Sector,
            match.LatestPrice,
            changePercent,
            historyDto,
            match.IsActive,
            match.TradingStatus,
            match.UniverseUpdatedAt);
    }

    private static decimal ChangePercentFromHistory(IReadOnlyList<StockRadar.Domain.Entities.OhlcvBar> history)
    {
        if (history.Count >= 2 && history[^2].Close > 0)
            return Math.Round((history[^1].Close - history[^2].Close) / history[^2].Close * 100m, 2);

        if (history.Count == 1 && history[0].Open > 0)
            return Math.Round((history[0].Close - history[0].Open) / history[0].Open * 100m, 2);

        return 0m;
    }

    public async Task<StockChartDto?> GetChartAsync(
        string symbol,
        string interval,
        CancellationToken cancellationToken = default)
    {
        var sym = symbol.Trim().ToUpperInvariant();
        var stock = await jobStocks.GetBySymbolAsync(sym, cancellationToken);
        if (stock is null)
            return null;

        var normalized = NormalizeInterval(interval);
        if (!chartBars.IsSupportedInterval(normalized))
            return null;

        var bars = await chartBars.FetchAsync(sym, normalized, cancellationToken);

        if (normalized.Equals("1D", StringComparison.OrdinalIgnoreCase))
        {
            var dbBars = stock.History
                .Where(b => TradingSessionMath.IsTradingDay(b.Date))
                .Select(b => new ChartBarDto(
                    b.Date.ToString("yyyy-MM-dd"),
                    b.Open,
                    b.High,
                    b.Low,
                    b.Close,
                    b.Volume))
                .ToList();

            if (dbBars.Count > bars.Count)
                bars = dbBars;
        }
        else if (bars.Count == 0)
        {
            bars = stock.History
                .Select(b => new ChartBarDto(
                    b.Date.ToString("yyyy-MM-dd"),
                    b.Open,
                    b.High,
                    b.Low,
                    b.Close,
                    b.Volume))
                .ToList();
        }

        return new StockChartDto(sym, normalized, bars);
    }

    private static string NormalizeInterval(string interval)
    {
        var value = interval.Trim();
        return value.ToUpperInvariant() switch
        {
            "1D" => "1D",
            "1H" => "1H",
            "30M" => "30m",
            "15M" => "15m",
            "5M" => "5m",
            "1M" => "1m",
            _ => value
        };
    }
}
