using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

public sealed class SignalAnalyzer : ISignalAnalyzer
{
    public const int Lookback = 20;

    public decimal GetChangePercent(IReadOnlyList<OhlcvBar> history, int days = 1) =>
        GetChangePercent(history, days, null);

    public decimal GetChangePercent(Stock stock, int days = 1) =>
        GetChangePercent(stock.History, days, stock.LastChangePercent);

    private static decimal GetChangePercent(
        IReadOnlyList<OhlcvBar> history,
        int days,
        decimal? syncedChangePercent)
    {
        if (history.Count >= days + 1)
        {
            var latest = history[^1].Close;
            var previous = history[^(days + 1)].Close;
            if (previous == 0)
                return 0;

            return Math.Round((latest - previous) / previous * 100m, 2);
        }

        if (syncedChangePercent is not null)
            return syncedChangePercent.Value;

        if (history.Count == 1)
        {
            var bar = history[0];
            if (bar.Open > 0)
                return Math.Round((bar.Close - bar.Open) / bar.Open * 100m, 2);
        }

        return 0;
    }

    public decimal GetAverageVolume(IReadOnlyList<OhlcvBar> history, int period = Lookback)
    {
        if (history.Count == 0)
            return 0;

        return (decimal)history.TakeLast(Math.Min(period, history.Count)).Average(b => b.Volume);
    }

    public decimal GetVolumeRatio(IReadOnlyList<OhlcvBar> history)
    {
        var avg = GetAverageVolume(history);
        if (avg <= 0 || history.Count == 0)
            return 1;

        return Math.Round(history[^1].Volume / avg, 2);
    }

    public decimal GetRelativeStrength(Stock stock, decimal indexChangePercent, int days = 5)
    {
        var stockChange = GetChangePercent(stock, days);
        return Math.Round(stockChange - indexChangePercent, 2);
    }

    public bool HasBullishMaStack(
        IReadOnlyList<OhlcvBar> history,
        bool enabled = true,
        int minSessionsForMa50 = 50,
        int minSessionsForFullStack = 200)
    {
        if (!enabled || history.Count < 20)
            return !enabled;

        var ma20 = MovingAverage(history, 20);
        if (history.Count < minSessionsForMa50)
            return ma20 >= history[^1].Close * 0.97m;

        var ma50 = MovingAverage(history, Math.Min(50, history.Count));
        if (history.Count < minSessionsForFullStack)
            return ma20 > ma50;

        var ma100 = MovingAverage(history, 100);
        var ma200 = MovingAverage(history, 200);
        return ma20 > ma50 && ma50 > ma100 && ma100 > ma200;
    }

    public bool HasValidBaseSetup(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings runup,
        decimal maxGainInBasePercent)
    {
        if (HasExceededMaxGainFromBase(history, runup))
            return false;

        var profile = AnalyzeBasePriceForFilter(history, runup);
        return profile is not null && profile.GainFromBasePercent <= maxGainInBasePercent;
    }

    private static decimal MovingAverage(IReadOnlyList<OhlcvBar> history, int period)
    {
        var count = Math.Min(period, history.Count);
        return history.TakeLast(count).Average(b => b.Close);
    }

    public decimal GetGainFromBasePercent(
        IReadOnlyList<OhlcvBar> history,
        decimal consolidationMaxRangePercent = 15m,
        int consolidationMinSessions = 5,
        int maxScanSessions = 90,
        decimal? currentPrice = null)
    {
        var profile = AnalyzeBasePrice(
            history,
            new BasePriceFilterSettings(
                consolidationMaxRangePercent,
                consolidationMinSessions,
                maxScanSessions),
            currentPrice);
        return profile?.GainFromBasePercent ?? 0;
    }

    public ConsolidationZone? FindNearestConsolidationZone(
        IReadOnlyList<OhlcvBar> history,
        decimal consolidationMaxRangePercent = 15m,
        int consolidationMinSessions = 5,
        int maxScanSessions = 90,
        decimal maxCloseDriftPercent = 8m)
    {
        var filter = new BasePriceFilterSettings(
            consolidationMaxRangePercent,
            consolidationMinSessions,
            maxScanSessions,
            MaxCloseDriftPercent: maxCloseDriftPercent);

        var segments = FindMaximalConsolidationSegments(
            history,
            filter.ConsolidationMaxRangePercent,
            filter.ConsolidationMinSessions,
            filter.MaxScanSessions,
            filter.MaxCloseDriftPercent);
        if (segments.Count == 0)
            return null;

        var clusters = BuildPriceClusters(segments, filter.MaxBandSeparationPercent);
        var current = history[^1].Close;
        var reference = SelectReferenceClusterForFilter(clusters, current);
        return ToConsolidationZone(reference);
    }

    private sealed class PriceBaseCluster
    {
        private readonly List<ConsolidationZone> _segments = [];

        public PriceBaseCluster(ConsolidationZone first) => _segments.Add(first);

        public IReadOnlyList<ConsolidationZone> Segments => _segments;

        public void Add(ConsolidationZone segment) => _segments.Add(segment);

        public decimal EnvelopeLow => _segments.Min(s => s.BaseLow);

        public decimal EnvelopeHigh => _segments.Max(s => s.BaseHigh);

        public decimal Midpoint => (EnvelopeLow + EnvelopeHigh) / 2m;

        public decimal AnchorMidpoint
        {
            get
            {
                var first = _segments[0];
                return (first.BaseLow + first.BaseHigh) / 2m;
            }
        }

        public int LatestEndIndex => _segments.Max(s => s.EndIndex);

        public decimal DistanceToPrice(decimal price)
        {
            if (price >= EnvelopeLow && price <= EnvelopeHigh)
                return 0;
            if (price > EnvelopeHigh)
                return price - EnvelopeHigh;
            return EnvelopeLow - price;
        }
    }

    private static List<PriceBaseCluster> BuildPriceClusters(
        IReadOnlyList<ConsolidationZone> segments,
        decimal maxBandSeparationPercent)
    {
        var clusters = new List<PriceBaseCluster>();
        foreach (var segment in segments.OrderBy(s => s.StartIndex))
        {
            var match = clusters.FirstOrDefault(c => SamePriceCluster(segment, c, maxBandSeparationPercent));
            if (match is null)
                clusters.Add(new PriceBaseCluster(segment));
            else
                match.Add(segment);
        }

        return clusters;
    }

    /// <summary>Hai đoạn cùng một nền khi midpoint gần điểm neo (đoạn đầu) — tránh gộp chuỗi leo giá.</summary>
    private static bool SamePriceCluster(
        ConsolidationZone segment,
        PriceBaseCluster cluster,
        decimal maxBandSeparationPercent)
    {
        var segMid = (segment.BaseLow + segment.BaseHigh) / 2m;
        var anchorMid = cluster.AnchorMidpoint;
        if (segMid <= 0 || anchorMid <= 0)
            return false;

        var separation = Math.Abs(segMid - anchorMid) / Math.Min(segMid, anchorMid) * 100m;
        return separation <= maxBandSeparationPercent;
    }

    /// <summary>UI: nền gần giá hiện tại trong cùng vùng giá.</summary>
    private static PriceBaseCluster SelectNearestCluster(
        IReadOnlyList<PriceBaseCluster> clusters,
        decimal currentPrice) =>
        clusters
            .OrderBy(c => c.DistanceToPrice(currentPrice))
            .ThenByDescending(c => c.LatestEndIndex)
            .First();

    /// <summary>Lọc FOMO: so với đỉnh nền thấp nhất mà giá hiện tại đã vượt qua.</summary>
    private static PriceBaseCluster SelectReferenceClusterForFilter(
        IReadOnlyList<PriceBaseCluster> clusters,
        decimal currentPrice)
    {
        const decimal insideTolerance = 0.001m;
        var inside = clusters
            .Where(c => currentPrice >= c.EnvelopeLow - insideTolerance
                && currentPrice <= c.EnvelopeHigh + insideTolerance)
            .ToList();
        if (inside.Count > 0)
            return SelectNearestCluster(inside, currentPrice);

        var below = clusters
            .Where(c => currentPrice > c.EnvelopeHigh)
            .OrderBy(c => c.EnvelopeHigh)
            .ThenByDescending(c => c.LatestEndIndex)
            .ToList();
        if (below.Count > 0)
            return below[0];

        return SelectNearestCluster(clusters, currentPrice);
    }

    private static ConsolidationZone ToConsolidationZone(PriceBaseCluster cluster)
    {
        var low = cluster.EnvelopeLow;
        var high = cluster.EnvelopeHigh;
        return new ConsolidationZone(
            cluster.Segments.Min(s => s.StartIndex),
            cluster.Segments.Max(s => s.EndIndex),
            low,
            high,
            low <= 0 ? 0 : Math.Round((high - low) / low * 100m, 2));
    }

    private static List<ConsolidationZone> FindMaximalConsolidationSegments(
        IReadOnlyList<OhlcvBar> history,
        decimal consolidationMaxRangePercent,
        int consolidationMinSessions,
        int maxScanSessions,
        decimal maxCloseDriftPercent = 8m)
    {
        var n = history.Count;
        var segments = new List<ConsolidationZone>();
        if (n < consolidationMinSessions)
            return segments;

        var scanStart = Math.Max(0, n - maxScanSessions);
        var i = scanStart;

        while (i <= n - consolidationMinSessions)
        {
            var low = history[i].Low;
            var high = history[i].High;
            var end = i;

            for (var j = i + 1; j < n; j++)
            {
                low = Math.Min(low, history[j].Low);
                high = Math.Max(high, history[j].High);
                if (low <= 0 || (high - low) / low * 100m > consolidationMaxRangePercent)
                    break;
                if (!IsSidewaysConsolidation(
                        history,
                        i,
                        j,
                        consolidationMaxRangePercent,
                        maxCloseDriftPercent))
                    break;
                end = j;
            }

            if (end - i + 1 >= consolidationMinSessions
                && IsSidewaysConsolidation(
                    history,
                    i,
                    end,
                    consolidationMaxRangePercent,
                    maxCloseDriftPercent))
            {
                segments.Add(new ConsolidationZone(
                    i,
                    end,
                    low,
                    high,
                    Math.Round((high - low) / low * 100m, 2)));
                i = end + 1;
            }
            else
            {
                i++;
            }
        }

        return segments;
    }

    private static bool IsSidewaysConsolidation(
        IReadOnlyList<OhlcvBar> history,
        int start,
        int end,
        decimal maxCloseRangePercent,
        decimal maxCloseDriftPercent)
    {
        if (start > end)
            return false;

        var startClose = history[start].Close;
        var endClose = history[end].Close;
        if (startClose <= 0)
            return false;

        var minClose = decimal.MaxValue;
        var maxClose = decimal.MinValue;
        for (var k = start; k <= end; k++)
        {
            minClose = Math.Min(minClose, history[k].Close);
            maxClose = Math.Max(maxClose, history[k].Close);
        }

        if (minClose <= 0)
            return false;

        if ((maxClose - minClose) / minClose * 100m > maxCloseRangePercent)
            return false;

        return Math.Abs(endClose - startClose) / startClose * 100m <= maxCloseDriftPercent;
    }

    public bool HasExceededMaxGainFromBase(
        IReadOnlyList<OhlcvBar> history,
        decimal maxGainPercent,
        decimal consolidationMaxRangePercent = 15m,
        int consolidationMinSessions = 5,
        int maxScanSessions = 90,
        decimal? currentPrice = null) =>
        GetGainFromBasePercent(
            history,
            consolidationMaxRangePercent,
            consolidationMinSessions,
            maxScanSessions,
            currentPrice) > maxGainPercent;

    public bool HasExceededMaxGainFromBase(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter,
        decimal? currentPrice = null)
    {
        var profile = AnalyzeBasePriceForFilter(history, filter, currentPrice);
        if (profile is null)
            return false;

        return profile.GainFromBasePercent > filter.MaxGainFromBasePercent;
    }

    /// <summary>Nền dùng cho lọc FOMO (khác UI khi giá đã markup).</summary>
    public BasePriceProfile? AnalyzeBasePriceForFilter(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter,
        decimal? currentPrice = null)
    {
        var clusters = BuildBaseClusters(history, filter);
        if (clusters is null || clusters.Count == 0)
            return null;

        var current = currentPrice ?? history[^1].Close;
        var reference = SelectReferenceClusterForFilter(clusters, current);
        return BuildBaseProfile(history, clusters, reference, current);
    }

    public BasePriceProfile? AnalyzeBasePrice(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter,
        decimal? currentPrice = null)
    {
        var clusters = BuildBaseClusters(history, filter);
        if (clusters is null || clusters.Count == 0)
            return null;

        var current = currentPrice ?? history[^1].Close;
        var nearest = SelectNearestCluster(clusters, current);
        return BuildBaseProfile(history, clusters, nearest, current);
    }

    private List<PriceBaseCluster>? BuildBaseClusters(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter)
    {
        var segments = FindMaximalConsolidationSegments(
            history,
            filter.ConsolidationMaxRangePercent,
            filter.ConsolidationMinSessions,
            filter.MaxScanSessions,
            filter.MaxCloseDriftPercent);
        if (segments.Count == 0)
            return null;

        return BuildPriceClusters(segments, filter.MaxBandSeparationPercent);
    }

    private static BasePriceProfile BuildBaseProfile(
        IReadOnlyList<OhlcvBar> history,
        IReadOnlyList<PriceBaseCluster> clusters,
        PriceBaseCluster selected,
        decimal currentPrice)
    {
        var orderedByPrice = clusters.OrderBy(c => c.Midpoint).ToList();
        var baseIndex = orderedByPrice.IndexOf(selected) + 1;
        var bandSegments = selected.Segments.OrderBy(s => s.StartIndex).ToList();

        var periods = bandSegments
            .Select(s => new BasePricePeriod(
                history[s.StartIndex].Date,
                history[s.EndIndex].Date,
                s.EndIndex - s.StartIndex + 1,
                s.BaseLow,
                s.BaseHigh))
            .ToList();

        var envelopeLow = bandSegments.Min(s => s.BaseLow);
        var envelopeHigh = bandSegments.Max(s => s.BaseHigh);
        var gain = envelopeHigh <= 0
            ? 0
            : Math.Round((currentPrice - envelopeHigh) / envelopeHigh * 100m, 2);

        return new BasePriceProfile(
            envelopeLow,
            envelopeHigh,
            periods.Sum(p => p.SessionDays),
            periods,
            gain,
            baseIndex,
            clusters.Count);
    }

    public bool IsBreakout(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < Lookback + 1)
            return false;

        var recent = history.TakeLast(Lookback + 1).ToList();
        var latest = recent[^1];
        var previousHigh = recent.Take(Lookback).Max(b => b.High);
        var avgVolume = recent.Take(Lookback).Average(b => (decimal)b.Volume);

        return latest.Close > previousHigh
               && latest.Volume > avgVolume * 2
               && GetChangePercent(history, 1) > 3;
    }

    public bool IsAccumulation(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < Lookback)
            return false;

        var bars = history.TakeLast(Lookback).ToList();
        var high = bars.Max(b => b.High);
        var low = bars.Min(b => b.Low);
        if (low <= 0)
            return false;

        if ((high - low) / low * 100m >= 15 || IsBreakout(history))
            return false;

        var firstHalfAvg = bars.Take(10).Average(b => (decimal)b.Volume);
        var secondHalfAvg = bars.Skip(10).Average(b => (decimal)b.Volume);
        return secondHalfAvg < firstHalfAvg;
    }

    public bool IsVolumeSpike(IReadOnlyList<OhlcvBar> history) =>
        GetVolumeRatio(history) >= 2;

    public bool IsDistribution(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < 5)
            return false;

        var bars = history.TakeLast(5).ToList();
        var volumeRising = bars[^1].Volume > bars[0].Volume;
        var priceFlat = Math.Abs(GetChangePercent(history, 5)) < 2;
        var upperWicks = bars.Count(b =>
        {
            var bodyTop = Math.Max(b.Open, b.Close);
            var upperWick = b.High - bodyTop;
            var body = Math.Abs(b.Close - b.Open);
            return upperWick > body && body > 0;
        }) >= 2;

        return volumeRising && priceFlat && upperWicks;
    }

    public bool IsShakeout(IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < Lookback + 2)
            return false;

        var support = history.TakeLast(Lookback + 2).Take(Lookback).Min(b => b.Low);
        var shake = history[^2];
        var recovery = history[^1];

        return shake.Low < support
               && shake.Volume < GetAverageVolume(history) * 1.2m
               && recovery.Close > support;
    }

    public IReadOnlyList<SignalType> DetectSignals(Stock stock, decimal indexChangePercent = 0)
    {
        var history = stock.History;
        var signals = new List<SignalType>();

        if (IsBreakout(history)) signals.Add(SignalType.Breakout);
        if (IsVolumeSpike(history)) signals.Add(SignalType.VolumeSpike);
        if (IsAccumulation(history)) signals.Add(SignalType.Accumulation);
        if (IsShakeout(history)) signals.Add(SignalType.Shakeout);
        if (IsDistribution(history)) signals.Add(SignalType.Distribution);
        if (GetRelativeStrength(stock, indexChangePercent, 5) > 3)
            signals.Add(SignalType.RelativeStrength);

        return signals;
    }

    public PriceLevels CalculatePriceLevels(IReadOnlyList<OhlcvBar> history)
    {
        var latest = history[^1];
        var support = history.TakeLast(Lookback).Min(b => b.Low);
        var resistance = history.TakeLast(Lookback).Max(b => b.High);
        var range = resistance - support;

        return new PriceLevels(
            Math.Round(latest.Close * 0.995m, 2),
            Math.Round(support * 0.98m, 2),
            Math.Round(resistance, 2),
            Math.Round(latest.Close + range * 0.8m, 2));
    }
}
