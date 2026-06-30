using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;

namespace StockRadar.Infrastructure.Persistence.Caching;

internal sealed class CachedStockRepository(
    IStockRepository inner,
    IMemoryCache cache,
    IOptions<CacheOptions> options) : IStockRepository
{
    private const string AllKey = "stocks:all";

    public async Task<IReadOnlyList<Stock>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
            return await inner.GetAllAsync(cancellationToken);

        return await cache.GetOrCreateAsync(AllKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(options.Value.StockListSeconds);
            return await inner.GetAllAsync(cancellationToken);
        }) ?? [];
    }

    public Task<Stock?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default) =>
        inner.GetBySymbolAsync(symbol, cancellationToken);

    public Task<IReadOnlyList<StockSummaryRow>> GetSummariesBySymbolsAsync(
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken = default) =>
        inner.GetSummariesBySymbolsAsync(symbols, cancellationToken);

    public Task<IReadOnlyList<string>> GetActiveSymbolsAsync(CancellationToken cancellationToken = default) =>
        inner.GetActiveSymbolsAsync(cancellationToken);
}

internal sealed class CachedMarketIndexProvider(
    IMarketIndexProvider inner,
    IMemoryCache cache,
    IOptions<CacheOptions> options) : IMarketIndexProvider
{
    private const string Key = "market:index";

    public Task<MarketIndex> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
            return inner.GetCurrentAsync(cancellationToken);

        return cache.GetOrCreateAsync(Key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(options.Value.MarketIndexSeconds);
            return await inner.GetCurrentAsync(cancellationToken);
        })!;
    }
}

internal static class CacheInvalidation
{
    public static void InvalidateStocks(IMemoryCache cache)
    {
        cache.Remove("stocks:all");
    }

    public static void InvalidateMarketData(IMemoryCache cache)
    {
        InvalidateStocks(cache);
        cache.Remove("market:index");
    }
}
