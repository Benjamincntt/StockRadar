using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

public sealed class SignalAnalyzer : ISignalAnalyzer
{
    public const int Lookback = 20;

    private readonly BaseQualityEvaluator _baseQuality = new();
    private readonly DarvasBreakoutAnalyzer _darvasBreakout = new();

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
        var profile = AnalyzeBasePriceForFilter(history, runup);
        if (profile is null)
            return false;

        if (profile.GainFromBasePercent > runup.MaxGainFromBasePercent)
            return false;

        return profile.GainFromBasePercent <= maxGainInBasePercent;
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
        var filter = new BasePriceFilterSettings(
            consolidationMinSessions,
            maxScanSessions);
        var profile = AnalyzeBasePrice(history, filter, currentPrice);
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
            consolidationMinSessions,
            maxScanSessions);
        var window = _baseQuality.FindBestBase(history, filter);
        if (window is null)
            return null;

        return new ConsolidationZone(
            window.StartIndex,
            window.EndIndex,
            window.BaseLow,
            window.BaseHigh,
            window.BaseLow <= 0
                ? 0
                : Math.Round((window.BaseHigh - window.BaseLow) / window.BaseLow * 100m, 2));
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
        var box = AnalyzeFlatBox(history, filter);
        if (!box.HasValidBox)
            return true;

        return box.GainFromBoxTopPercent > filter.MaxGainFromBasePercent;
    }

    /// <summary>Nền dùng cho lọc FOMO (ưu tiên vùng giá gần giá hiện tại / đã breakout).</summary>
    public BasePriceProfile? AnalyzeBasePriceForFilter(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter,
        decimal? currentPrice = null)
    {
        var window = _baseQuality.FindBestBase(history, filter, preferBreakoutReference: true);
        return window is null ? null : ToProfile(history, window, currentPrice, filter);
    }

    public BasePriceProfile? AnalyzeBasePrice(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter,
        decimal? currentPrice = null)
    {
        var tops = _baseQuality.FindTopBases(history, filter, maxCount: 3);
        if (tops.Count == 0)
            return null;

        var current = currentPrice ?? history[^1].Close;
        var selected = tops
            .OrderBy(w => DistanceToEnvelope(w, current))
            .ThenByDescending(w => w.Quality.TotalScore)
            .ThenByDescending(w => w.EndIndex)
            .First();

        return ToProfile(history, selected, current, filter, tops);
    }

    private static decimal DistanceToEnvelope(BaseQualityEvaluator.BaseWindow window, decimal price)
    {
        if (price >= window.BaseLow && price <= window.BaseHigh)
            return 0;
        if (price > window.BaseHigh)
            return price - window.BaseHigh;
        return window.BaseLow - price;
    }

    private BasePriceProfile ToProfile(
        IReadOnlyList<OhlcvBar> history,
        BaseQualityEvaluator.BaseWindow window,
        decimal? currentPrice,
        BasePriceFilterSettings filter,
        IReadOnlyList<BaseQualityEvaluator.BaseWindow>? allBases = null)
    {
        var current = currentPrice ?? history[^1].Close;
        var periods = _baseQuality.BuildChartPeriods(
            history,
            window.StartIndex,
            window.EndIndex);

        var gain = window.BaseHigh <= 0
            ? 0
            : Math.Round((current - window.BaseHigh) / window.BaseHigh * 100m, 2);

        var bases = allBases ?? [window];
        var orderedByMid = bases
            .OrderBy(b => (b.BaseLow + b.BaseHigh) / 2m)
            .ToList();
        var baseIndex = orderedByMid.IndexOf(window) + 1;

        return new BasePriceProfile(
            window.BaseLow,
            window.BaseHigh,
            window.EndIndex - window.StartIndex + 1,
            periods,
            gain,
            baseIndex,
            bases.Count,
            window.Quality.TotalScore,
            window.Quality);
    }

    public FlatBoxProfile AnalyzeFlatBox(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter) =>
        _darvasBreakout.Analyze(history, filter);

    public DarvasBreakoutResult EvaluateDarvasBreakout(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter) =>
        _darvasBreakout.Evaluate(history, filter);

    public bool IsDarvasBreakout(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter) =>
        AnalyzeFlatBox(history, filter).IsBreakoutConfirmed;

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

    /// <summary>Rũ qua đáy nền rồi hồi phục trên đáy nền (KL shake thấp).</summary>
    public bool IsShakeoutFromBase(IReadOnlyList<OhlcvBar> history, BasePriceFilterSettings filter)
    {
        var box = AnalyzeFlatBox(history, filter);
        if (!box.HasValidBox || history.Count < 4)
            return false;

        var baseLow = box.BoxLow;
        const int lookback = 8;
        var window = history.TakeLast(Math.Min(lookback + 1, history.Count)).ToList();
        var latest = window[^1];
        if (latest.Close <= baseLow)
            return false;

        var prior = window.Take(window.Count - 1).ToList();
        var shakeBars = prior.Where(b => b.Low < baseLow).ToList();
        if (shakeBars.Count == 0)
            return false;

        var avgVol = GetAverageVolume(history);
        return shakeBars.Any(b => b.Volume < avgVol * 1.2m);
    }

    public bool MeetsSessionEntryBar(
        IReadOnlyList<OhlcvBar> history,
        decimal minChangePercent,
        decimal minSessionVolume) =>
        history.Count > 0
        && GetChangePercent(history, 1) > minChangePercent
        && history[^1].Volume >= minSessionVolume;

    public IReadOnlyList<SignalType> DetectSignals(
        Stock stock,
        decimal indexChangePercent = 0,
        BasePriceFilterSettings? runup = null)
    {
        var history = stock.History;
        var filter = runup ?? new BasePriceFilterSettings();
        var signals = new List<SignalType>();

        if (IsBreakout(history)) signals.Add(SignalType.Breakout);
        if (IsDarvasBreakout(history, filter)) signals.Add(SignalType.DarvasBreakout);
        if (IsVolumeSpike(history)) signals.Add(SignalType.VolumeSpike);
        if (IsAccumulation(history)) signals.Add(SignalType.Accumulation);
        if (IsShakeoutFromBase(history, filter)) signals.Add(SignalType.Shakeout);
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
