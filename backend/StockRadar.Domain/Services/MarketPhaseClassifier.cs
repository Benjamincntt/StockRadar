using StockRadar.Domain.Entities;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

/// <summary>
/// Phân loại pha tăng trưởng (Favorable / Neutral=Attempted Rally / Unfavorable=Correction).
/// Độc lập với <c>MarketRegime</c> ReversalBounce — không gộp hai hệ.
/// </summary>
public static class MarketPhaseClassifier
{
    public static MarketPhaseClassification Classify(
        IReadOnlyList<OhlcvBar> history,
        MarketPhaseThresholds? thresholds = null)
    {
        var t = thresholds ?? MarketPhaseThresholds.Default;

        if (history.Count < 20)
        {
            return new MarketPhaseClassification(
                MarketWyckoffPhase.Unfavorable,
                CloseAboveMa20: false,
                Ma20SlopeNonNegative: false,
                HasFollowThroughDay: false,
                FollowThroughDate: null,
                HasHigherLow: false,
                RallyDayOne: null);
        }

        var close = history[^1].Close;
        var ma20 = SmaClose(history, 20);
        var aboveMa20 = close > ma20;
        var slopeOk = Ma20SlopeNonNegative(history, t.Ma20SlopeLookbackSessions);
        var (hasFtd, ftdDate, dayOne) = FindFollowThroughDay(history, t);
        var hasHl = HasHigherLow(history, t.HigherLowLookbackSessions, t.HigherLowPivotRadius);

        if (!aboveMa20)
        {
            return new MarketPhaseClassification(
                MarketWyckoffPhase.Unfavorable,
                CloseAboveMa20: false,
                Ma20SlopeNonNegative: slopeOk,
                HasFollowThroughDay: hasFtd,
                FollowThroughDate: ftdDate,
                HasHigherLow: hasHl,
                RallyDayOne: dayOne);
        }

        if (slopeOk && hasFtd && hasHl)
        {
            return new MarketPhaseClassification(
                MarketWyckoffPhase.Favorable,
                CloseAboveMa20: true,
                Ma20SlopeNonNegative: true,
                HasFollowThroughDay: true,
                FollowThroughDate: ftdDate,
                HasHigherLow: true,
                RallyDayOne: dayOne);
        }

        return new MarketPhaseClassification(
            MarketWyckoffPhase.Neutral,
            CloseAboveMa20: true,
            Ma20SlopeNonNegative: slopeOk,
            HasFollowThroughDay: hasFtd,
            FollowThroughDate: ftdDate,
            HasHigherLow: hasHl,
            RallyDayOne: dayOne);
    }

    private static (bool HasFtd, DateOnly? FtdDate, DateOnly? DayOne) FindFollowThroughDay(
        IReadOnlyList<OhlcvBar> history,
        MarketPhaseThresholds t)
    {
        // Quét mọi đợt nỗ lực trong lookback dài: FTD xác nhận vẫn có hiệu lực khi đang trên MA20.
        var searchStart = Math.Max(1, history.Count - Math.Max(t.RallyLookbackSessions * 3, 60));
        DateOnly? bestDayOne = null;

        for (var dayOneIdx = searchStart; dayOneIdx < history.Count - t.FtdMinRallyDay; dayOneIdx++)
        {
            if (history[dayOneIdx].Close <= history[dayOneIdx - 1].Close)
                continue;

            // Bắt đầu nỗ lực: phiên tăng sau ít nhất một phiên giảm.
            if (dayOneIdx >= 2 && history[dayOneIdx - 1].Close >= history[dayOneIdx - 2].Close)
                continue;

            bestDayOne ??= history[dayOneIdx].Date;

            for (var day = t.FtdMinRallyDay; day <= t.FtdMaxRallyDay; day++)
            {
                var ftdIdx = dayOneIdx + day - 1;
                if (ftdIdx <= 0 || ftdIdx >= history.Count)
                    continue;

                var bar = history[ftdIdx];
                var prev = history[ftdIdx - 1];
                if (prev.Close <= 0)
                    continue;

                var gain = (bar.Close - prev.Close) / prev.Close * 100m;
                if (gain < t.FtdMinGainPercent)
                    continue;

                if (bar.Volume <= prev.Volume)
                    continue;

                var avgVol = AverageVolumeBefore(history, ftdIdx, 20);
                if (avgVol <= 0 || bar.Volume <= avgVol)
                    continue;

                return (true, bar.Date, history[dayOneIdx].Date);
            }
        }

        return (false, null, bestDayOne);
    }

    private static bool HasHigherLow(IReadOnlyList<OhlcvBar> history, int lookback, int radius)
    {
        var start = Math.Max(radius, history.Count - lookback);
        var end = history.Count - radius;
        if (end - start < radius * 2 + 2)
            return false;

        var pivotLows = new List<decimal>();
        for (var i = start; i < end; i++)
        {
            var low = history[i].Low;
            var isPivot = true;
            for (var j = i - radius; j <= i + radius; j++)
            {
                if (j == i)
                    continue;
                if (history[j].Low < low)
                {
                    isPivot = false;
                    break;
                }
            }

            if (isPivot)
                pivotLows.Add(low);
        }

        for (var i = 1; i < pivotLows.Count; i++)
        {
            if (pivotLows[i] > pivotLows[i - 1])
                return true;
        }

        return false;
    }

    private static bool Ma20SlopeNonNegative(IReadOnlyList<OhlcvBar> history, int lookback)
    {
        if (history.Count < 20 + lookback)
            return true;

        var maNow = SmaClose(history, 20);
        var older = history.Take(history.Count - lookback).ToList();
        var maPrev = SmaClose(older, 20);
        return maNow >= maPrev;
    }

    private static decimal SmaClose(IReadOnlyList<OhlcvBar> history, int period)
    {
        var count = Math.Min(period, history.Count);
        return history.TakeLast(count).Average(b => b.Close);
    }

    private static decimal AverageVolumeBefore(IReadOnlyList<OhlcvBar> history, int beforeIdx, int period)
    {
        if (beforeIdx <= 0)
            return 0m;

        var take = Math.Min(period, beforeIdx);
        var sum = 0m;
        for (var i = beforeIdx - take; i < beforeIdx; i++)
            sum += history[i].Volume;
        return sum / take;
    }
}

public sealed record MarketPhaseClassification(
    MarketWyckoffPhase Phase,
    bool CloseAboveMa20,
    bool Ma20SlopeNonNegative,
    bool HasFollowThroughDay,
    DateOnly? FollowThroughDate,
    bool HasHigherLow,
    DateOnly? RallyDayOne);
