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
    ISectorWaveRegimeRepository sectorWaveRegimes,
    IMemoryCache cache,
    IOptions<CacheOptions> cacheOptions,
    IOptions<SmartMoneyOptions> smartMoneyOptions,
    IOptions<PriceRunupFilterOptions> runupFilter)
{
    private const string ContextCacheKey = "smartmoney:context";
    private static readonly SemaphoreSlim ContextLock = new(1, 1);

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

        if (cache.TryGetValue<SmartMoneyMarketContext>(ContextCacheKey, out var cached) && cached is not null)
            return cached;

        await ContextLock.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue<SmartMoneyMarketContext>(ContextCacheKey, out cached) && cached is not null)
                return cached;

            var ctx = await BuildContextCoreAsync(cancellationToken);
            cache.Set(ContextCacheKey, ctx, TimeSpan.FromSeconds(cfg.SmartMoneyContextSeconds));
            return ctx;
        }
        finally
        {
            ContextLock.Release();
        }
    }

    private async Task<SmartMoneyMarketContext> BuildContextCoreAsync(
        CancellationToken cancellationToken)
    {
        var all = await stocks.GetAllAsync(cancellationToken);
        var index = await marketIndex.GetCurrentAsync(cancellationToken);
        var adaptive = await adaptiveProfileFactory.LoadAsync(cancellationToken);
        var calibration = await hitCalibrationProfileFactory.LoadAsync(cancellationToken);
        var context = selector.BuildContext(
            all,
            index,
            runupFilter.Value.ToSettings(),
            smartMoneyOptions.Value.ToSettings(),
            adaptive,
            calibration);

        var activeRegimes = await LoadActiveSectorRegimesAsync(context.SectorSnapshots.Keys, cancellationToken);
        return context with { ActiveSectorRegimes = activeRegimes };
    }

    /// <summary>
    /// Đọc read-only trạng thái Sóng ngành xuyên phiên (spec 007) đã persist bởi DailyAnalysisRunner —
    /// KHÔNG tính/ghi lại ở đây, chỉ để trang chi tiết mã đồng bộ với gate Top thực tế.
    /// </summary>
    private async Task<HashSet<string>> LoadActiveSectorRegimesAsync(
        IEnumerable<string> sectors,
        CancellationToken cancellationToken)
    {
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sector in sectors)
        {
            var latest = await sectorWaveRegimes.GetLatestAsync(sector, cancellationToken);
            if (latest is { IsActive: true })
                active.Add(sector);
        }

        return active;
    }

    public SmartMoneyEvaluation EvaluateStock(Stock stock, SmartMoneyMarketContext context) =>
        selector.Evaluate(stock, context);
}
