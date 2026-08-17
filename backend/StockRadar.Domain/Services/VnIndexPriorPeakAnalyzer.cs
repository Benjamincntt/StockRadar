using StockRadar.Domain.Entities;

namespace StockRadar.Domain.Services;

/// <summary>
/// Đỉnh kháng cự VNINDEX (swing high + prominence) — gate bull trap khi hồi sát đỉnh mà pha chưa Favorable.
/// </summary>
public static class VnIndexPriorPeakAnalyzer
{
    public sealed record PriorPeak(DateOnly Date, decimal Price, decimal ProminencePercent);

    /// <summary>
    /// Swing high trong lookback vẫn nằm trên <paramref name="livePrice"/>,
    /// đủ prominence; chọn đỉnh <b>gần nhất theo giá</b> (min PeakPrice &gt; live), không theo ngày.
    /// </summary>
    public static PriorPeak? FindActiveResistance(
        IReadOnlyList<OhlcvBar> history,
        DateOnly sessionDate,
        decimal livePrice,
        int lookbackSessions = 750,
        int pivotRadius = 2,
        decimal minProminencePercent = 3m)
    {
        if (history.Count == 0 || livePrice <= 0 || lookbackSessions < 5 || pivotRadius < 1)
            return null;

        var prior = history.Where(b => b.Date < sessionDate).ToList();
        if (prior.Count < pivotRadius * 2 + 3)
            return null;

        var start = Math.Max(pivotRadius, prior.Count - lookbackSessions);
        PriorPeak? best = null;

        for (var i = start; i < prior.Count - pivotRadius; i++)
        {
            if (!IsSwingHigh(prior, i, pivotRadius))
                continue;

            var peakPrice = prior[i].High;
            if (peakPrice <= livePrice)
                continue;

            var prominence = ProminenceAfterPeak(prior, i, peakPrice);
            if (prominence < minProminencePercent)
                continue;

            var candidate = new PriorPeak(prior[i].Date, peakPrice, Math.Round(prominence, 2));
            // Đỉnh gần nhất theo giá (kháng cự sát trên live), không theo thời gian.
            if (best is null || candidate.Price < best.Price)
                best = candidate;
        }

        return best;
    }

    public static bool IsNearPriorPeak(
        PriorPeak? peak,
        decimal livePrice,
        decimal bandPercent = 1.5m)
    {
        if (peak is null || livePrice <= 0 || peak.Price <= livePrice || bandPercent < 0)
            return false;

        var distancePct = (peak.Price - livePrice) / peak.Price * 100m;
        return distancePct <= bandPercent;
    }

    /// <summary>
    /// Môi trường bull-trap: sát đỉnh cũ + pha ≠ Favorable.
    /// Trong env này VIP chỉ cho Buy1 dip-bounce và Buy2 scale-in (+%), không cho nổ breakout.
    /// </summary>
    public static bool IsBullTrapEnvironment(
        bool gateEnabled,
        bool nearPriorPeak,
        string? marketPhase)
    {
        if (!gateEnabled || !nearPriorPeak)
            return false;

        return !string.Equals(marketPhase, nameof(MarketWyckoffPhase.Favorable), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Alias cũ — dùng <see cref="IsBullTrapEnvironment"/>.</summary>
    public static bool ShouldBlockBuy(
        bool gateEnabled,
        bool nearPriorPeak,
        string? marketPhase) =>
        IsBullTrapEnvironment(gateEnabled, nearPriorPeak, marketPhase);

    /// <summary>
    /// "Near peak" có hysteresis — chữa nhiễu khi live dao động quanh mép band (VD ~1.773 nếu
    /// đỉnh 1.800 + band 1.5%): bật ở <paramref name="enterBandPercent"/>, chỉ tắt khi lùi xa hơn
    /// <paramref name="exitBandPercent"/>. Chỉ áp dụng khi live còn <b>dưới</b> đỉnh (approach từ
    /// dưới) — live đã xuyên đỉnh (≥ peak) là trap-context (pin), không phải hysteresis.
    /// </summary>
    public static bool IsNearPriorPeakWithHysteresis(
        PriorPeak? peak,
        decimal livePrice,
        bool wasActive,
        decimal enterBandPercent,
        decimal exitBandPercent)
    {
        if (peak is null || livePrice <= 0 || peak.Price <= livePrice)
            return false;

        var distancePct = (peak.Price - livePrice) / peak.Price * 100m;
        var enterBand = Math.Max(0m, enterBandPercent);

        if (!wasActive)
            return distancePct <= enterBand;

        var exitBand = Math.Max(enterBand, exitBandPercent);
        return distancePct <= exitBand;
    }

    private static bool IsSwingHigh(IReadOnlyList<OhlcvBar> bars, int i, int radius)
    {
        var high = bars[i].High;
        for (var j = i - radius; j <= i + radius; j++)
        {
            if (j == i)
                continue;
            if (bars[j].High > high)
                return false;
        }

        return true;
    }

    private static decimal ProminenceAfterPeak(IReadOnlyList<OhlcvBar> bars, int peakIdx, decimal peakPrice)
    {
        if (peakPrice <= 0 || peakIdx >= bars.Count - 1)
            return 0m;

        var minLow = bars[peakIdx + 1].Low;
        for (var i = peakIdx + 2; i < bars.Count; i++)
            minLow = Math.Min(minLow, bars[i].Low);

        if (minLow <= 0 || minLow >= peakPrice)
            return 0m;

        return (peakPrice - minLow) / peakPrice * 100m;
    }
}
