using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

public sealed class WatchlistService(
    IWatchlistRepository watchlist,
    IStockRepository stocks,
    IDailyOpportunityRepository dailyOpportunities,
    SmartMoneyEvaluationService smartMoneyEval,
    IBuyDecisionEngine buyDecision) : IWatchlistService
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

        // Mã ngoài Top: Buy Score live — cùng engine với StockService detail (không dùng Criterion Composite).
        Dictionary<string, int>? liveScores = null;
        var missing = summarySymbols.Where(s => !opportunityScores.ContainsKey(s)).ToList();
        if (missing.Count > 0)
        {
            liveScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var context = await smartMoneyEval.BuildContextAsync(cancellationToken);
            foreach (var symbol in missing)
            {
                var stock = await stocks.GetBySymbolAsync(symbol, cancellationToken);
                if (stock is null)
                    continue;
                liveScores[symbol] = buyDecision.Evaluate(stock, context).BuyScore;
            }
        }

        var items = summaries.Select(summary =>
        {
            var score = opportunityScores.TryGetValue(summary.Symbol, out var oppScore)
                ? oppScore
                : liveScores?.GetValueOrDefault(summary.Symbol) ?? 0;

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
