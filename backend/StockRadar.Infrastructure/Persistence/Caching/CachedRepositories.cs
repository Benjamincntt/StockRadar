using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;

namespace StockRadar.Infrastructure.Persistence.Caching;

internal sealed class CachedStockRepository(
    IJobStockRepository inner,
    IMemoryCache cache,
    IOptions<CacheOptions> options) : IStockRepository, IJobStockRepository
{
    private const string AllKey = "stocks:all";

    public async Task<IReadOnlyList<Stock>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
            return await inner.GetAllAsync(cancellationToken);

        return await cache.GetOrCreateAsync(AllKey, async entry =>
        {
            entry.AddExpirationToken(new CancellationChangeToken(CacheInvalidation.StocksToken));
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(options.Value.StockListSeconds);
            return await inner.GetAllAsync(cancellationToken);
        }) ?? [];
    }

    public async Task<Stock?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
            return await inner.GetBySymbolAsync(symbol, cancellationToken);

        var key = $"stocks:sym:{symbol.ToUpperInvariant()}";
        return await cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AddExpirationToken(new CancellationChangeToken(CacheInvalidation.StocksToken));
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(options.Value.StockListSeconds);
            return await inner.GetBySymbolAsync(symbol, cancellationToken);
        });
    }

    public Task<IReadOnlyList<StockSummaryRow>> GetSummariesBySymbolsAsync(
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken = default) =>
        inner.GetSummariesBySymbolsAsync(symbols, cancellationToken);

    public Task<IReadOnlyList<string>> GetActiveSymbolsAsync(CancellationToken cancellationToken = default) =>
        inner.GetActiveSymbolsAsync(cancellationToken);

    public Task<IReadOnlyList<Stock>> GetAllForUniverseScreeningAsync(
        CancellationToken cancellationToken = default) =>
        inner.GetAllForUniverseScreeningAsync(cancellationToken);
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
    private const string SmartMoneyContextKey = "smartmoney:context";
    private static CancellationTokenSource _stocksCts = new();

    public static CancellationToken StocksToken => _stocksCts.Token;

    public static void InvalidateStocks(IMemoryCache cache)
    {
        cache.Remove(AllStocksKey);
        var old = Interlocked.Exchange(ref _stocksCts, new CancellationTokenSource());
        old.Cancel();
        old.Dispose();
    }

    public static void InvalidateMarketData(IMemoryCache cache)
    {
        InvalidateStocks(cache);
        cache.Remove("market:index");
        cache.Remove(SmartMoneyContextKey);
    }

    private const string AllStocksKey = "stocks:all";
}
