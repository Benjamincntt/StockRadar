using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.MarketData;
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

    // Hysteresis quanh mép near-peak (approach từ dưới) — độc lập với pin, chỉ áp dụng khi live < peak.
    private bool _hysteresisActive;

    // Window-integrity (slice 2, opt-in): đã từng thủng pin (live < pin) từ 13:00 trở đi chưa.
    private bool _pinWindowIntegrityBroken;

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
                ResetOnRolloverIfNeeded(sessionDate);
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
                ResetOnRolloverIfNeeded(sessionDate);
                _sessionDate = sessionDate;
                _peak = peak;
                _livePrice = live;
                _loaded = true;

                // Hysteresis (slice 2, độc lập pin): approach từ dưới chỉ, chống flicker mép band.
                _hysteresisActive = cfg.BullTrapHysteresisEnabled
                    ? VnIndexPriorPeakAnalyzer.IsNearPriorPeakWithHysteresis(
                        peak, live, _hysteresisActive, cfg.BullTrapNearPeakBandPercent, cfg.BullTrapNearPeakExitBandPercent)
                    : VnIndexPriorPeakAnalyzer.IsNearPriorPeak(peak, live, cfg.BullTrapNearPeakBandPercent);

                // Ghim mốc trap khi env LẦN ĐẦU bật (sát đỉnh dưới band). Ghi một lần/phiên,
                // xoá theo rollover ở trên. Dùng để phát hiện xuyên sau khi _peak xoay/null.
                if (_trapPeakPinned <= 0m && _hysteresisActive)
                    _trapPeakPinned = peak!.Price;

                // Window-integrity (slice 2, opt-in): từ 13:00, nếu đã ghim mà live rơi dưới pin
                // dù chỉ một tick → thủng, không tự phục hồi trong phiên.
                if (_trapPeakPinned > 0m
                    && VietnamMarketCalendar.NowVietnam().TimeOfDay >= new TimeSpan(13, 0, 0)
                    && live > 0m
                    && live < _trapPeakPinned)
                {
                    _pinWindowIntegrityBroken = true;
                }
            }
        }
        catch
        {
            // Fail-open: thiếu VNINDEX → không chặn Buy.
            lock (_gate)
            {
                ResetOnRolloverIfNeeded(sessionDate);
                _sessionDate = sessionDate;
                _peak = null;
                _livePrice = 0;
                _loaded = true;
            }
        }
    }

    /// <summary>Reset state theo phiên trước khi ghi đè _sessionDate. Gọi trong lock.</summary>
    private void ResetOnRolloverIfNeeded(DateOnly sessionDate)
    {
        if (_sessionDate == sessionDate)
            return;

        _trapPeakPinned = 0m;
        _hysteresisActive = false;
        _pinWindowIntegrityBroken = false;
    }

    public bool IsNearPriorPeak(DateOnly sessionDate)
    {
        lock (_gate)
        {
            if (!_loaded || _sessionDate != sessionDate || _livePrice <= 0)
                return false;

            return _hysteresisActive;
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

    /// <summary>
    /// Window-integrity (slice 2, opt-in): true nếu chưa từng thủng pin từ 13:00 tới giờ (hoặc pin
    /// chưa ghim — vacuously true, không phải mối lo của cờ này). False khi đã có tick live &lt; pin.
    /// </summary>
    public bool PinWindowIntegrityHeld(DateOnly sessionDate)
    {
        lock (_gate)
        {
            return _sessionDate != sessionDate || !_pinWindowIntegrityBroken;
        }
    }
}
