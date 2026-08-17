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

    // Trap-context theo phiên: ghim mốc đỉnh khi env LẦN ĐẦU bật trong phiên.
    // _peak ephemeral (ghi đè mỗi vòng, xoay/null khi xuyên); _trapPeakPinned bền để phát hiện xuyên.
    private decimal _trapPeakPinned;

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
                if (_sessionDate != sessionDate) _trapPeakPinned = 0m;
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
                if (_sessionDate != sessionDate) _trapPeakPinned = 0m;
                _sessionDate = sessionDate;
                _peak = peak;
                _livePrice = live;
                _loaded = true;

                // Ghim mốc trap khi env LẦN ĐẦU bật (sát đỉnh dưới band). Ghi một lần/phiên,
                // xoá theo rollover ở trên. Dùng để phát hiện xuyên sau khi _peak xoay/null.
                if (_trapPeakPinned <= 0m
                    && VnIndexPriorPeakAnalyzer.IsNearPriorPeak(peak, live, cfg.BullTrapNearPeakBandPercent))
                {
                    _trapPeakPinned = peak!.Price;
                }
            }
        }
        catch
        {
            // Fail-open: thiếu VNINDEX → không chặn Buy.
            lock (_gate)
            {
                if (_sessionDate != sessionDate) _trapPeakPinned = 0m;
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

    /// <summary>Env đã từng bật trong phiên (mốc trap đã ghim). Trap-context còn sống.</summary>
    public bool TrapContextActive(DateOnly sessionDate)
    {
        lock (_gate)
        {
            return _sessionDate == sessionDate && _trapPeakPinned > 0m;
        }
    }

    /// <summary>Index live đã xuyên (≥) mốc trap đã ghim — env tắt vì trên đỉnh, nhưng vẫn là trap-zone.</summary>
    public bool LiveAbovePin(DateOnly sessionDate)
    {
        lock (_gate)
        {
            return _sessionDate == sessionDate
                && _trapPeakPinned > 0m
                && _livePrice > 0m
                && _livePrice >= _trapPeakPinned;
        }
    }
}
