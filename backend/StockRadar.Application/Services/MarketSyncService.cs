using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Enums;

namespace StockRadar.Application.Services;

public sealed class MarketSyncService(
    IMarketDataWriter writer,
    IStockRepository stocks,
    IMarketRealtimePublisher publisher,
    IQuoteTickCache quoteCache) : IMarketSyncService
{
    public async Task<MarketSyncResultDto> ApplyAsync(
        MarketSyncRequest request,
        CancellationToken cancellationToken = default)
    {
        var indexUpdated = false;
        if (request.Index is not null && request.Index.Price > 0)
        {
            await writer.UpsertIndexAsync(request.Index, cancellationToken);
            var indexTick = ToIndexTick(request.Index);
            quoteCache.SetIndex(indexTick);
            await publisher.PublishIndexAsync(indexTick, cancellationToken);
            indexUpdated = true;
        }

        var stocksUpdated = 0;
        if (request.Quotes.Count > 0)
        {
            stocksUpdated = await writer.UpsertQuotesAsync(request.Quotes, cancellationToken);
            var ticks = request.Quotes
                .Where(q => !string.IsNullOrWhiteSpace(q.Symbol) && q.Close > 0)
                .Select(ToQuoteTick)
                .ToList();
            if (ticks.Count > 0)
            {
                quoteCache.SetQuotes(ticks);
                await publisher.PublishQuotesAsync(ticks, cancellationToken);
            }
        }

        return new MarketSyncResultDto(stocksUpdated, indexUpdated, DateTime.UtcNow);
    }

    public async Task<IReadOnlyList<string>> GetTrackedSymbolsAsync(
        CancellationToken cancellationToken = default)
    {
        var all = await stocks.GetAllAsync(cancellationToken);
        return all.Select(s => s.Symbol).OrderBy(s => s).ToList();
    }

    private static QuoteTickDto ToQuoteTick(StockQuoteSyncDto quote) =>
        new(
            quote.Symbol.Trim().ToUpperInvariant(),
            quote.Close,
            quote.ChangePercent,
            quote.Volume,
            DateTime.UtcNow);

    private static IndexTickDto ToIndexTick(MarketIndexSyncDto index)
    {
        var trend = index.ChangePercent switch
        {
            > 0.5m => MarketTrend.Uptrend,
            < -0.5m => MarketTrend.Downtrend,
            _ => MarketTrend.Sideway
        };
        var score = Math.Clamp(50 + (int)(index.ChangePercent * 10), 0, 100);
        var symbol = string.IsNullOrWhiteSpace(index.Symbol) ? "VNINDEX" : index.Symbol.ToUpperInvariant();

        return new IndexTickDto(
            symbol,
            index.Price,
            index.ChangePercent,
            score,
            trend.ToString(),
            DateTime.UtcNow);
    }
}
