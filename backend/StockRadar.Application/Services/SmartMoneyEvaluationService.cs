using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Services;

public sealed class SmartMoneyEvaluationService(
    IJobStockRepository stocks,
    IJobMarketIndexProvider marketIndex,
    ISmartMoneyOpportunitySelector selector,
    AdaptiveScoringProfileFactory adaptiveProfileFactory,
    HitCalibrationProfileFactory hitCalibrationProfileFactory,
    IMemoryCache cache,
    IOptions<CacheOptions> cacheOptions,
    IOptions<SmartMoneyOptions> smartMoneyOptions,
    IOptions<PriceRunupFilterOptions> runupFilter)
{
    private const string ContextCacheKey = "smartmoney:context";

    public async Task<(SmartMoneyMarketContext Context, SmartMoneyEvaluation Eval)?> EvaluateAsync(
        Stock stock,
        CancellationToken cancellationToken = default)
    {
        var context = await BuildContextAsync(cancellationToken);
        return (context, selector.Evaluate(stock, context));
    }

    public async Task<SmartMoneyMarketContext> BuildContextAsync(
        CancellationToken cancellationToken = default)
    {
        var cfg = cacheOptions.Value;
        if (!cfg.Enabled)
            return await BuildContextCoreAsync(cancellationToken);

        return await cache.GetOrCreateAsync(ContextCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cfg.SmartMoneyContextSeconds);
            return await BuildContextCoreAsync(cancellationToken);
        }) ?? await BuildContextCoreAsync(cancellationToken);
    }

    private async Task<SmartMoneyMarketContext> BuildContextCoreAsync(
        CancellationToken cancellationToken)
    {
        var all = await stocks.GetAllAsync(cancellationToken);
        var index = await marketIndex.GetCurrentAsync(cancellationToken);
        var adaptive = await adaptiveProfileFactory.LoadAsync(cancellationToken);
        var calibration = await hitCalibrationProfileFactory.LoadAsync(cancellationToken);
        return selector.BuildContext(
            all,
            index,
            runupFilter.Value.ToSettings(),
            smartMoneyOptions.Value.ToSettings(),
            adaptive,
            calibration);
    }

    public SmartMoneyEvaluation EvaluateStock(Stock stock, SmartMoneyMarketContext context) =>
        selector.Evaluate(stock, context);
}