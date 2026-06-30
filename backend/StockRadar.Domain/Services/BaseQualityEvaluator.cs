using StockRadar.Domain.Entities;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

/// <summary>
/// Base Quality Engine: pipeline gate (loại sideway rác) → chấm điểm compression / ATR / volume khô.
/// Không dùng tư duy "đi ngang X phiên = nền".
/// </summary>
public sealed class BaseQualityEvaluator
{
    private const int AtrPeriod = 14;
    private const int CompressionSegments = 4;
    private const int SwingLookback = 2;

    public sealed record BaseWindow(
        int StartIndex,
        int EndIndex,
        decimal BaseLow,
        decimal BaseHigh,
        BaseQualityComponents Quality);

    public BaseWindow? FindBestBase(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter,
        bool preferBreakoutReference = false)
    {
        var n = history.Count;
        if (n < filter.ConsolidationMinSessions + 10)
            return null;

        var scanStart = Math.Max(0, n - filter.MaxScanSessions);
        var maxLen = Math.Min(filter.MaxBaseWindowSessions, n - scanStart);
        var minLen = filter.ConsolidationMinSessions;
        var endMin = Math.Max(scanStart + minLen - 1, n - 3);
        var endMax = n - 1;
        var current = history[^1].Close;

        BaseWindow? best = null;
        BaseWindow? bestFilter = null;

        for (var end = endMax; end >= endMin; end--)
        {
            var maxWindowLen = Math.Min(maxLen, end - scanStart + 1);
            for (var len = minLen; len <= maxWindowLen; len++)
            {
                var start = end - len + 1;
                if (!PassesPipelineGates(history, start, end, filter))
                    continue;

                var quality = ScoreWindow(history, start, end, filter);
                if (quality.TotalScore < filter.MinBaseQualityScore)
                    continue;

                var (low, high) = WindowEnvelope(history, start, end);
                var candidate = new BaseWindow(start, end, low, high, quality);

                if (best is null || IsBetterCandidate(candidate, best))
                    best = candidate;

                if (preferBreakoutReference
                    && current >= low
                    && (bestFilter is null || IsBetterFilterCandidate(candidate, bestFilter, current)))
                    bestFilter = candidate;
            }
        }

        return preferBreakoutReference ? bestFilter ?? best : best;
    }

    public IReadOnlyList<BaseWindow> FindTopBases(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter,
        int maxCount = 3)
    {
        var n = history.Count;
        if (n < filter.ConsolidationMinSessions + 10)
            return [];

        var scanStart = Math.Max(0, n - filter.MaxScanSessions);
        var maxLen = Math.Min(filter.MaxBaseWindowSessions, n - scanStart);
        var minLen = filter.ConsolidationMinSessions;
        var endMin = Math.Max(scanStart + minLen - 1, n - 3);
        var endMax = n - 1;

        var candidates = new List<BaseWindow>();
        for (var end = endMax; end >= endMin; end--)
        {
            var maxWindowLen = Math.Min(maxLen, end - scanStart + 1);
            for (var len = minLen; len <= maxWindowLen; len++)
            {
                var start = end - len + 1;
                if (!PassesPipelineGates(history, start, end, filter))
                    continue;

                var quality = ScoreWindow(history, start, end, filter);
                if (quality.TotalScore < filter.MinBaseQualityScore)
                    continue;

                var (low, high) = WindowEnvelope(history, start, end);
                candidates.Add(new BaseWindow(start, end, low, high, quality));
            }
        }

        return SelectNonOverlapping(candidates, maxCount);
    }

    public BaseQualityComponents ScoreWindow(
        IReadOnlyList<OhlcvBar> history,
        int start,
        int end,
        BasePriceFilterSettings filter)
    {
        return new BaseQualityComponents(
            ScorePriorTrend(history, start, filter),
            ScoreAtrContraction(history, start, end),
            ScoreCompression(history, start, end),
            ScoreVolumeDry(history, start, end),
            ScoreContractionPattern(history, start, end),
            ScoreDistribution(history, start, end),
            ScoreDuration(end - start + 1, filter));
    }

    public IReadOnlyList<BasePricePeriod> BuildChartPeriods(
        IReadOnlyList<OhlcvBar> history,
        int start,
        int end)
    {
        if (start > end)
            return [];

        return [MakePeriod(history, start, end)];
    }

    /// <summary>Pipeline gate — tất cả bước phải pass mới coi là nền.</summary>
    private static bool PassesPipelineGates(
        IReadOnlyList<OhlcvBar> history,
        int start,
        int end,
        BasePriceFilterSettings filter)
    {
        var sessions = end - start + 1;
        if (sessions < filter.ConsolidationMinSessions)
            return false;

        if (!HasPriorUptrend(history, start, filter))
            return false;

        if (!HasAtrContraction(history, start, end))
            return false;

        if (!HasVolumeDryUp(history, start, end))
            return false;

        if (!HasSwingCompression(history, start, end))
            return false;

        if (!HasVolatilityContractionPattern(history, start, end))
            return false;

        if (HasDistributionBars(history, start, end))
            return false;

        if (IsPingPongSideway(history, start, end))
            return false;

        var (low, high) = WindowEnvelope(history, start, end);
        if (low <= 0)
            return false;

        var width = (high - low) / low * 100m;
        if (width > 18m)
            return false;

        var netDrift = (history[end].Close - history[start].Close) / history[start].Close * 100m;
        if (netDrift < -4m)
            return false;

        return true;
    }

    private static bool HasPriorUptrend(
        IReadOnlyList<OhlcvBar> history,
        int baseStart,
        BasePriceFilterSettings filter)
    {
        var lookback = filter.PriorImpulseLookbackSessions;
        var preStart = Math.Max(0, baseStart - lookback);
        if (baseStart - preStart < 10)
            return false;

        var impulseLow = decimal.MaxValue;
        for (var i = preStart; i < baseStart; i++)
            impulseLow = Math.Min(impulseLow, history[i].Low);

        if (impulseLow <= 0)
            return false;

        var anchor = history[baseStart].Close;
        var impulseGain = (anchor - impulseLow) / impulseLow * 100m;
        if (impulseGain < filter.MinPriorImpulsePercent)
            return false;

        var emaEarly = EmaAt(history, 20, preStart + 5);
        var emaAtBase = EmaAt(history, 20, baseStart);
        if (emaEarly <= 0 || emaAtBase <= emaEarly)
            return false;

        var midPre = preStart + (baseStart - preStart) / 2;
        var preFirstHalf = AverageClose(history, preStart, midPre);
        var preSecondHalf = AverageClose(history, midPre + 1, baseStart - 1);
        if (preFirstHalf <= 0 || preSecondHalf <= preFirstHalf * 1.03m)
            return false;

        return anchor >= EmaAt(history, 50, baseStart) * 0.97m;
    }

    private static bool HasAtrContraction(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        var quarters = GetSegmentRanges(start, end, 4);
        if (quarters.Count < 4)
            return false;

        var atrs = quarters
            .Select(q => AverageAtr(history, q.Start, q.End, AtrPeriod))
            .ToList();

        if (atrs.Any(a => a <= 0))
            return false;

        var declines = 0;
        for (var i = 1; i < atrs.Count; i++)
        {
            if (atrs[i] < atrs[i - 1] * 0.97m)
                declines++;
        }

        return declines >= 2 && atrs[^1] < atrs[0] * 0.80m;
    }

    private static bool HasVolumeDryUp(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        var volMa5 = VolumeMaAt(history, end, 5);
        var volMa20 = VolumeMaAt(history, end, 20);
        if (volMa5 <= 0 || volMa20 <= 0 || volMa5 >= volMa20)
            return false;

        var len = end - start + 1;
        var firstThird = AverageVolume(history, start, start + len / 3);
        var lastThird = AverageVolume(history, end - len / 3 + 1, end);
        return lastThird < firstThird * 0.85m;
    }

    private static bool HasSwingCompression(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        var swings = MeasureSegmentSwings(history, start, end, CompressionSegments);
        if (swings.Count < 3)
            return false;

        var decreases = 0;
        for (var i = 1; i < swings.Count; i++)
        {
            if (swings[i] < swings[i - 1] * 0.92m)
                decreases++;
        }

        return decreases >= swings.Count - 2
            && swings[^1] < swings[0] * 0.60m;
    }

    private static bool HasVolatilityContractionPattern(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        var len = end - start + 1;
        var third = Math.Max(2, len / 3);

        var highFirst = AverageHigh(history, start, start + third - 1);
        var highLast = AverageHigh(history, end - third + 1, end);
        var lowFirst = AverageLow(history, start, start + third - 1);
        var lowLast = AverageLow(history, end - third + 1, end);

        if (highFirst <= 0 || lowFirst <= 0)
            return false;

        var highsFalling = highLast < highFirst * 0.985m;
        var lowsRising = lowLast > lowFirst * 1.01m;

        var swingHighs = FindSwingHighs(history, start, end);
        var swingLows = FindSwingLows(history, start, end);
        var wedgeOk = swingHighs.Count >= 2
            && swingLows.Count >= 2
            && swingHighs[^1] < swingHighs[0] * 0.99m
            && swingLows[^1] > swingLows[0] * 1.01m;

        return (highsFalling && lowsRising) || wedgeOk;
    }

    private static bool HasDistributionBars(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        var avgVol = AverageVolume(history, start, end);
        if (avgVol <= 0)
            return false;

        for (var i = start + 1; i <= end; i++)
        {
            var prevClose = history[i - 1].Close;
            if (prevClose <= 0)
                continue;

            var change = (history[i].Close - prevClose) / prevClose * 100m;
            if (change <= -6m && history[i].Volume > avgVol * 1.5m)
                return true;
        }

        return false;
    }

    /// <summary>Sideway ping-pong: chạm cả trên và dưới mà không co biên.</summary>
    private static bool IsPingPongSideway(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        var (low, high) = WindowEnvelope(history, start, end);
        var band = high - low;
        if (band <= 0)
            return true;

        var topFloor = high - band * 0.20m;
        var bottomCeil = low + band * 0.20m;

        var topTouches = 0;
        var bottomTouches = 0;
        for (var i = start; i <= end; i++)
        {
            if (history[i].High >= topFloor)
                topTouches++;
            if (history[i].Low <= bottomCeil)
                bottomTouches++;
        }

        if (topTouches < 2 || bottomTouches < 2)
            return false;

        var swings = MeasureSegmentSwings(history, start, end, CompressionSegments);
        if (swings.Count < 2)
            return true;

        var compressionRatio = swings[^1] / Math.Max(swings[0], 0.01m);
        return compressionRatio > 0.75m;
    }

    private static int ScorePriorTrend(
        IReadOnlyList<OhlcvBar> history,
        int baseStart,
        BasePriceFilterSettings filter)
    {
        var preStart = Math.Max(0, baseStart - filter.PriorImpulseLookbackSessions);
        var impulseLow = decimal.MaxValue;
        for (var i = preStart; i < baseStart; i++)
            impulseLow = Math.Min(impulseLow, history[i].Low);

        if (impulseLow <= 0)
            return 0;

        var gain = (history[baseStart].Close - impulseLow) / impulseLow * 100m;
        if (gain >= 40m) return 100;
        if (gain >= 30m) return 90;
        if (gain >= 25m) return 80;
        if (gain >= 20m) return 70;
        if (gain >= filter.MinPriorImpulsePercent) return 60;
        return 30;
    }

    private static int ScoreAtrContraction(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        var quarters = GetSegmentRanges(start, end, 4);
        var atrs = quarters.Select(q => AverageAtr(history, q.Start, q.End, AtrPeriod)).ToList();
        if (atrs.Count < 2 || atrs[0] <= 0)
            return 0;

        var ratio = atrs[^1] / atrs[0];
        var declines = 0;
        for (var i = 1; i < atrs.Count; i++)
        {
            if (atrs[i] < atrs[i - 1])
                declines++;
        }

        var score = ratio switch
        {
            < 0.50m => 100,
            < 0.65m => 90,
            < 0.75m => 80,
            < 0.85m => 65,
            _ => 40
        };

        if (declines >= 3)
            score = Math.Min(100, score + 10);

        return score;
    }

    private static int ScoreCompression(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        var swings = MeasureSegmentSwings(history, start, end, CompressionSegments);
        if (swings.Count < 2)
            return 0;

        var decreases = 0;
        for (var i = 1; i < swings.Count; i++)
        {
            if (swings[i] < swings[i - 1])
                decreases++;
        }

        var ratio = swings[^1] / Math.Max(swings[0], 0.01m);
        var score = ratio switch
        {
            < 0.35m => 100,
            < 0.50m => 90,
            < 0.60m => 80,
            < 0.75m => 60,
            _ => 35
        };

        if (decreases >= swings.Count - 1)
            score = Math.Min(100, score + 15);

        return score;
    }

    private static int ScoreVolumeDry(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        var volMa5 = VolumeMaAt(history, end, 5);
        var volMa20 = VolumeMaAt(history, end, 20);
        if (volMa20 <= 0)
            return 0;

        var ratio = volMa5 / volMa20;
        var len = end - start + 1;
        var firstHalf = AverageVolume(history, start, start + len / 2);
        var secondHalf = AverageVolume(history, start + len / 2 + 1, end);

        var score = ratio switch
        {
            < 0.55m => 100,
            < 0.70m => 85,
            < 0.85m => 70,
            _ => 45
        };

        if (firstHalf > 0 && secondHalf < firstHalf * 0.75m)
            score = Math.Min(100, score + 10);

        return score;
    }

    private static int ScoreContractionPattern(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        var len = end - start + 1;
        var third = Math.Max(2, len / 3);

        var highFirst = AverageHigh(history, start, start + third - 1);
        var highLast = AverageHigh(history, end - third + 1, end);
        var lowFirst = AverageLow(history, start, start + third - 1);
        var lowLast = AverageLow(history, end - third + 1, end);

        if (highFirst <= 0 || lowFirst <= 0)
            return 0;

        var highDrop = (highFirst - highLast) / highFirst * 100m;
        var lowRise = (lowLast - lowFirst) / lowFirst * 100m;
        var score = 40;

        if (highDrop > 0)
            score += (int)Math.Min(30, highDrop * 8);
        if (lowRise > 0)
            score += (int)Math.Min(30, lowRise * 8);

        return Math.Min(100, score);
    }

    private static int ScoreDistribution(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        var avgVol = AverageVolume(history, start, end);
        if (avgVol <= 0)
            return 80;

        var badBars = 0;
        for (var i = start + 1; i <= end; i++)
        {
            var prevClose = history[i - 1].Close;
            if (prevClose <= 0)
                continue;

            var change = (history[i].Close - prevClose) / prevClose * 100m;
            if (change <= -4m && history[i].Volume > avgVol * 1.3m)
                badBars++;
        }

        return badBars switch
        {
            0 => 100,
            1 => 50,
            _ => 0
        };
    }

    private static int ScoreDuration(int sessionCount, BasePriceFilterSettings filter)
    {
        if (sessionCount < filter.ConsolidationMinSessions)
            return 0;

        if (sessionCount >= filter.IdealBaseMinSessions && sessionCount <= filter.IdealBaseMaxSessions)
            return 100;

        if (sessionCount < filter.IdealBaseMinSessions)
            return 55 + (sessionCount - filter.ConsolidationMinSessions) * 5;

        return Math.Max(50, 90 - (sessionCount - filter.IdealBaseMaxSessions));
    }

    private static List<decimal> MeasureSegmentSwings(
        IReadOnlyList<OhlcvBar> history,
        int start,
        int end,
        int segments)
    {
        var ranges = GetSegmentRanges(start, end, segments);
        var swings = new List<decimal>();
        foreach (var seg in ranges)
        {
            var low = decimal.MaxValue;
            var high = decimal.MinValue;
            for (var i = seg.Start; i <= seg.End; i++)
            {
                low = Math.Min(low, history[i].Low);
                high = Math.Max(high, history[i].High);
            }

            if (low > 0)
                swings.Add((high - low) / low * 100m);
        }

        return swings;
    }

    private static List<(int Start, int End)> GetSegmentRanges(int start, int end, int segments)
    {
        var len = end - start + 1;
        var ranges = new List<(int Start, int End)>();
        for (var s = 0; s < segments; s++)
        {
            var segStart = start + s * len / segments;
            var segEnd = s == segments - 1 ? end : start + (s + 1) * len / segments - 1;
            ranges.Add((segStart, segEnd));
        }

        return ranges;
    }

    private static List<decimal> FindSwingHighs(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        var highs = new List<decimal>();
        for (var i = start + SwingLookback; i <= end - SwingLookback; i++)
        {
            var isHigh = true;
            for (var j = i - SwingLookback; j <= i + SwingLookback; j++)
            {
                if (j == i)
                    continue;
                if (history[j].High > history[i].High)
                {
                    isHigh = false;
                    break;
                }
            }

            if (isHigh)
                highs.Add(history[i].High);
        }

        return highs;
    }

    private static List<decimal> FindSwingLows(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        var lows = new List<decimal>();
        for (var i = start + SwingLookback; i <= end - SwingLookback; i++)
        {
            var isLow = true;
            for (var j = i - SwingLookback; j <= i + SwingLookback; j++)
            {
                if (j == i)
                    continue;
                if (history[j].Low < history[i].Low)
                {
                    isLow = false;
                    break;
                }
            }

            if (isLow)
                lows.Add(history[i].Low);
        }

        return lows;
    }

    private static bool IsBetterCandidate(BaseWindow candidate, BaseWindow current)
    {
        var scoreDiff = candidate.Quality.TotalScore - current.Quality.TotalScore;
        if (scoreDiff > 5)
            return true;
        if (scoreDiff < -5)
            return false;

        var candLen = candidate.EndIndex - candidate.StartIndex;
        var currLen = current.EndIndex - current.StartIndex;
        if (candLen != currLen)
            return candLen > currLen;

        return candidate.EndIndex > current.EndIndex;
    }

    private static bool IsBetterFilterCandidate(BaseWindow candidate, BaseWindow current, decimal price)
    {
        var candDist = DistanceToEnvelope(candidate, price);
        var currDist = DistanceToEnvelope(current, price);
        if (candDist != currDist)
            return candDist < currDist;

        return candidate.Quality.TotalScore > current.Quality.TotalScore;
    }

    private static decimal DistanceToEnvelope(BaseWindow window, decimal price)
    {
        if (price >= window.BaseLow && price <= window.BaseHigh)
            return 0;
        if (price > window.BaseHigh)
            return price - window.BaseHigh;
        return window.BaseLow - price;
    }

    private static List<BaseWindow> SelectNonOverlapping(List<BaseWindow> candidates, int maxCount)
    {
        var ordered = candidates
            .OrderByDescending(c => c.Quality.TotalScore)
            .ThenByDescending(c => c.EndIndex)
            .ToList();

        var selected = new List<BaseWindow>();
        foreach (var c in ordered)
        {
            if (selected.Any(s => Overlaps(s, c)))
                continue;
            selected.Add(c);
            if (selected.Count >= maxCount)
                break;
        }

        return selected;
    }

    private static bool Overlaps(BaseWindow a, BaseWindow b) =>
        a.StartIndex <= b.EndIndex && b.StartIndex <= a.EndIndex;

    private static BasePricePeriod MakePeriod(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        var (low, high) = WindowEnvelope(history, start, end);
        return new BasePricePeriod(
            history[start].Date,
            history[end].Date,
            end - start + 1,
            low,
            high);
    }

    private static (decimal Low, decimal High) WindowEnvelope(
        IReadOnlyList<OhlcvBar> history,
        int start,
        int end)
    {
        var low = decimal.MaxValue;
        var high = decimal.MinValue;
        for (var i = start; i <= end; i++)
        {
            low = Math.Min(low, history[i].Low);
            high = Math.Max(high, history[i].High);
        }

        return (low, high);
    }

    private static decimal AverageClose(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        if (start > end)
            return 0;

        var sum = 0m;
        for (var i = start; i <= end; i++)
            sum += history[i].Close;
        return sum / (end - start + 1);
    }

    private static decimal AverageHigh(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        if (start > end)
            return 0;

        var sum = 0m;
        for (var i = start; i <= end; i++)
            sum += history[i].High;
        return sum / (end - start + 1);
    }

    private static decimal AverageLow(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        if (start > end)
            return 0;

        var sum = 0m;
        for (var i = start; i <= end; i++)
            sum += history[i].Low;
        return sum / (end - start + 1);
    }

    private static decimal AverageVolume(IReadOnlyList<OhlcvBar> history, int start, int end)
    {
        if (start > end)
            return 0;

        var sum = 0m;
        for (var i = start; i <= end; i++)
            sum += history[i].Volume;
        return sum / (end - start + 1);
    }

    private static decimal AtrAt(IReadOnlyList<OhlcvBar> history, int index, int period)
    {
        if (history.Count < 2 || index < 1)
            return 0;

        var start = Math.Max(1, index - period + 1);
        var sum = 0m;
        var count = 0;
        for (var i = start; i <= index; i++)
        {
            sum += TrueRange(history, i);
            count++;
        }

        return count == 0 ? 0 : sum / count;
    }

    private static decimal AverageAtr(
        IReadOnlyList<OhlcvBar> history,
        int start,
        int end,
        int period)
    {
        if (start > end)
            return 0;

        var sum = 0m;
        var count = 0;
        for (var i = start; i <= end; i++)
        {
            var atr = AtrAt(history, i, period);
            if (atr > 0)
            {
                sum += atr;
                count++;
            }
        }

        return count == 0 ? 0 : sum / count;
    }

    private static decimal TrueRange(IReadOnlyList<OhlcvBar> history, int index)
    {
        var bar = history[index];
        var prevClose = history[index - 1].Close;
        return Math.Max(
            bar.High - bar.Low,
            Math.Max(Math.Abs(bar.High - prevClose), Math.Abs(bar.Low - prevClose)));
    }

    private static decimal EmaAt(IReadOnlyList<OhlcvBar> history, int period, int index)
    {
        if (history.Count == 0 || index < 0)
            return 0;

        var start = Math.Max(0, index - period * 3);
        var k = 2m / (period + 1);
        var ema = history[start].Close;
        for (var i = start + 1; i <= index; i++)
            ema = history[i].Close * k + ema * (1 - k);
        return ema;
    }

    private static decimal VolumeMaAt(IReadOnlyList<OhlcvBar> history, int index, int period)
    {
        var start = Math.Max(0, index - period + 1);
        return AverageVolume(history, start, index);
    }
}
