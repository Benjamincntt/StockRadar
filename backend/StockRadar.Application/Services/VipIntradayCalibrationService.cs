using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.Options;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Services;

public sealed class VipIntradayCalibrationService(
    IServiceScopeFactory scopeFactory,
    IVipIntradayCalibrationStore store,
    IOptions<MasterAlertOptions> options,
    IOptions<OpportunityPerformanceOptions> performanceOptions) : IVipIntradayCalibrationService
{
    private HitCalibrationProfile _profile = HitCalibrationProfile.Default;
    private readonly object _gate = new();
    private bool _loaded;

    public HitCalibrationProfile GetProfile()
    {
        EnsureLoaded();
        lock (_gate)
            return _profile;
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var loaded = await store.LoadAsync(cancellationToken);
        lock (_gate)
        {
            _profile = loaded ?? HitCalibrationProfile.Default;
            _loaded = true;
        }
    }

    public async Task<HitCalibrationProfile> RebuildAsync(CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        var lookback = Math.Clamp(cfg.DynamicThresholdLookbackDays, 7, 180);
        var today = TradingCalendar.TodayVietnam();
        var from = TradingSessionMath.SubtractTradingSessions(today, lookback);
        var threshold = performanceOptions.Value.SuccessThresholdPercent;

        await using var scope = scopeFactory.CreateAsyncScope();
        var fires = scope.ServiceProvider.GetRequiredService<IVipAlertFireRepository>();
        var rows = await fires.GetSinceAsync(from, cancellationToken);
        var samples = rows
            .Where(r => r.IntradayMeasured
                        && r.IntradayReturnPercent.HasValue
                        && r.MlProbAtFire is > 0)
            .Select(r => new SetupPredictionSample(
                r.MlProbAtFire!.Value,
                r.IntradayReturnPercent!.Value >= threshold))
            .ToList();

        var profile = HitCalibrationBuilder.Build(samples);
        await store.SaveAsync(profile, cancellationToken);
        lock (_gate)
        {
            _profile = profile;
            _loaded = true;
        }

        return profile;
    }

    private void EnsureLoaded()
    {
        if (_loaded)
            return;
        lock (_gate)
        {
            if (_loaded)
                return;
            _profile = store.LoadAsync().GetAwaiter().GetResult() ?? HitCalibrationProfile.Default;
            _loaded = true;
        }
    }
}
