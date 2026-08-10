using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.Options;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

/// <summary>Ngưỡng MinMlProb động theo hit-rate gần đây (Phase 4).</summary>
public sealed class VipIntradayThresholdService(
    IServiceScopeFactory scopeFactory,
    IOptions<MasterAlertOptions> options,
    IOptions<OpportunityPerformanceOptions> performanceOptions) : IVipIntradayThresholdService
{
    private readonly object _gate = new();
    private decimal _bump;

    public decimal ResolveMinMlProb(string marketPhase)
    {
        var cfg = options.Value;
        if (!cfg.MinMlProbToFire.TryGetValue(marketPhase, out var min)
            && !cfg.MinMlProbToFire.TryGetValue("Neutral", out min))
            min = 52m;

        if (!cfg.DynamicThresholdEnabled)
            return min;

        lock (_gate)
            return Math.Clamp(min + _bump, 30m, 85m);
    }

    public async Task RefreshFromKpiAsync(CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        if (!cfg.DynamicThresholdEnabled)
        {
            lock (_gate)
                _bump = 0;
            return;
        }

        var lookback = Math.Clamp(cfg.DynamicThresholdLookbackDays, 7, 180);
        var today = TradingCalendar.TodayVietnam();
        var from = TradingSessionMath.SubtractTradingSessions(today, lookback);
        var threshold = performanceOptions.Value.SuccessThresholdPercent;

        await using var scope = scopeFactory.CreateAsyncScope();
        var fires = scope.ServiceProvider.GetRequiredService<IVipAlertFireRepository>();
        var rows = await fires.GetSinceAsync(from, cancellationToken);
        var measured = rows
            .Where(r => r.IntradayMeasured && r.IntradayReturnPercent.HasValue)
            .ToList();

        if (measured.Count < 10)
        {
            lock (_gate)
                _bump = 0;
            return;
        }

        var hits = measured.Count(r => r.IntradayReturnPercent!.Value >= threshold);
        var hitRate = 100m * hits / measured.Count;
        var bump = hitRate < cfg.DynamicHitRateFloorPercent ? cfg.DynamicThresholdBump : 0m;

        lock (_gate)
            _bump = bump;
    }
}
