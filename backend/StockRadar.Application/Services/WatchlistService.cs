using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

public sealed class WatchlistService(
    IWatchlistRepository watchlist,
    IStockRepository stocks,
    SmartMoneyEvaluationService smartMoneyEval,
    ISignalAnalyzer signalAnalyzer) : IWatchlistService
{
    public async Task<IReadOnlyList<WatchlistItemDto>> GetItemsAsync(
        CancellationToken cancellationToken = default)
    {
        var symbols = await watchlist.GetSymbolsAsync(cancellationToken);
        var all = await stocks.GetAllAsync(cancellationToken);
        var context = await smartMoneyEval.BuildContextAsync(cancellationToken);
        var items = new List<WatchlistItemDto>();

        foreach (var symbol in symbols)
        {
            var stock = all.FirstOrDefault(s => s.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            if (stock is null)
                continue;

            var eval = smartMoneyEval.EvaluateStock(stock, context);
            items.Add(new WatchlistItemDto(
                stock.Symbol,
                stock.Name,
                stock.Sector,
                eval.Score,
                signalAnalyzer.GetChangePercent(stock, 1),
                stock.SectorLocked));
        }

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
