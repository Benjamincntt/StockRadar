using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;

namespace StockRadar.Application.Services;

public sealed class WatchlistService(
    IWatchlistRepository watchlist,
    IStockRepository stocks,
    IDailyOpportunityRepository dailyOpportunities,
    ICriterionScoringRepository criterionScores) : IWatchlistService
{
    public async Task<IReadOnlyList<WatchlistItemDto>> GetItemsAsync(
        CancellationToken cancellationToken = default)
    {
        var symbols = await watchlist.GetSymbolsAsync(cancellationToken);
        if (symbols.Count == 0)
            return [];

        var summaries = await stocks.GetSummariesBySymbolsAsync(symbols, cancellationToken);
        if (summaries.Count == 0)
            return [];

        var summarySymbols = summaries.Select(s => s.Symbol).ToList();
        var oppDate = TradingCalendar.GetActiveOpportunityDate();
        var opportunityScores = await dailyOpportunities.GetScoresBySymbolsForDateAsync(
            oppDate,
            summarySymbols,
            cancellationToken);

        var asOfDate = await criterionScores.GetLatestAccuracyDateAsync(cancellationToken: cancellationToken);
        var criterionScoreMap = asOfDate is null
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : await criterionScores.GetCompositeScoresBySymbolsAsync(
                asOfDate.Value,
                summarySymbols,
                cancellationToken);

        var items = summaries.Select(summary =>
        {
            var score = opportunityScores.TryGetValue(summary.Symbol, out var oppScore)
                ? oppScore
                : criterionScoreMap.GetValueOrDefault(summary.Symbol);

            return new WatchlistItemDto(
                summary.Symbol,
                summary.Name,
                summary.Sector,
                score,
                summary.LastChangePercent,
                summary.SectorLocked);
        });

        return items.OrderByDescending(w => w.Score).ToList();
    }

    public async Task<bool> AddAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var stock = await stocks.GetBySymbolAsync(symbol, cancellationToken)
            ?? throw new AppException("Not Found", $"Không tìm thấy mã {symbol.ToUpperInvariant()}", 404);

        if (await watchlist.ContainsAsync(stock.Symbol, cancellationToken))
            return false;

        await watchlist.AddAsync(stock.Symbol, cancellationToken);
        return true;
    }

    public async Task<bool> RemoveAsync(string symbol, CancellationToken cancellationToken = default)
    {
        if (!await watchlist.ContainsAsync(symbol, cancellationToken))
            return false;

        await watchlist.RemoveAsync(symbol, cancellationToken);
        return true;
    }
}
