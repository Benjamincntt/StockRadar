using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Services;
using Microsoft.Extensions.Options;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>Cache đỉnh kháng cự VNINDEX theo phiên — prefetch mỗi vòng monitor.</summary>
internal sealed class VipVnIndexPeakCache(IOptions<MasterAlertOptions> options)
{
    private readonly object _gate = new();
    private DateOnly _sessionDate;
    private VnIndexPriorPeakAnalyzer.PriorPeak? _peak;
    private decimal _livePrice;
    private bool _loaded;

    public async Task PrefetchAsync(
        DateOnly sessionDate,
        IJobMarketIndexProvider marketIndex,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        if (!cfg.BullTrapGateEnabled)
        {
            lock (_gate)
            {
                _sessionDate = sessionDate;
                _peak = null;
                _livePrice = 0;
                _loaded = true;
            }

            return;
        }

        try
        {
            var index = await marketIndex.GetCurrentAsync(cancellationToken);
            var live = index.Price > 0 ? index.Price : index.Bars.LastOrDefault()?.Close ?? 0m;
            var peak = VnIndexPriorPeakAnalyzer.FindActiveResistance(
                index.Bars,
                sessionDate,
                live,
                cfg.BullTrapPeakLookbackSessions,
                cfg.BullTrapPivotRadius,
                cfg.BullTrapMinProminencePercent);

            lock (_gate)
            {
                _sessionDate = sessionDate;
                _peak = peak;
                _livePrice = live;
                _loaded = true;
            }
        }
        catch
        {
            // Fail-open: thiếu VNINDEX → không chặn Buy.
            lock (_gate)
            {
                _sessionDate = sessionDate;
                _peak = null;
                _livePrice = 0;
                _loaded = true;
            }
        }
    }

    public bool IsNearPriorPeak(DateOnly sessionDate)
    {
        var cfg = options.Value;
        lock (_gate)
        {
            if (!_loaded || _sessionDate != sessionDate || _livePrice <= 0)
                return false;

            return VnIndexPriorPeakAnalyzer.IsNearPriorPeak(
                _peak,
                _livePrice,
                cfg.BullTrapNearPeakBandPercent);
        }
    }

    public (VnIndexPriorPeakAnalyzer.PriorPeak? Peak, decimal LivePrice) Snapshot(DateOnly sessionDate)
    {
        lock (_gate)
        {
            if (_sessionDate != sessionDate)
                return (null, 0m);
            return (_peak, _livePrice);
        }
    }
}
