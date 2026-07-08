using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

public interface ITrendSetupEvaluator
{
    bool HasValidTrendSetup(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings runup,
        SmartMoneySettings smartMoney);

    CriterionForwardOutcome MeasureOutcome(
        IReadOnlyList<OhlcvBar> stockHistory,
        int asOfIdx,
        int forwardSessions,
        decimal baseLow,
        PatternBias bias,
        IReadOnlyList<OhlcvBar> indexHistory,
        CriterionAccuracySettings settings);

    MarketWyckoffPhase ClassifyMarketPhase(
        IReadOnlyList<OhlcvBar> indexHistory,
        int asOfIdx);

    string GetScoreBucket(int score);
}

public sealed class TrendSetupEvaluator(ISignalAnalyzer signals) : ITrendSetupEvaluator
{
    /// <summary>Horizon DB = 2 → đo giá T+2.5 (TB đóng T+2, T+3), khớp North Star / OpportunityPerformance.</summary>
    private const int T25HorizonSessions = 2;

    private const int T25MfeWindowSessions = 3;

    public bool HasValidTrendSetup(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings runup,
        SmartMoneySettings smartMoney)
    {
        var flatBox = signals.AnalyzeFlatBox(history, runup);
        if (!flatBox.HasValidBox)
            return false;

        if (flatBox.GainFromBoxTopPercent > runup.MaxGainFromBasePercent)
            return false;

        if (!signals.MeetsSessionEntryBar(
                history,
                smartMoney.MinSessionChangePercent,
                smartMoney.MinSessionVolume))
            return false;

        var stock = new Stock("", "", "", history);
        var detected = signals.DetectSignals(stock, 0m, runup);
        return flatBox.IsBreakoutConfirmed
            || detected.Contains(SignalType.DarvasBreakout)
            || signals.IsShakeoutFromBase(history, runup);
    }

    public CriterionForwardOutcome MeasureOutcome(
        IReadOnlyList<OhlcvBar> stockHistory,
        int asOfIdx,
        int forwardSessions,
        decimal baseLow,
        PatternBias bias,
        IReadOnlyList<OhlcvBar> indexHistory,
        CriterionAccuracySettings settings) =>
        forwardSessions == T25HorizonSessions
            ? MeasureOutcomeT25(stockHistory, asOfIdx, baseLow, bias, indexHistory, settings)
            : MeasureOutcomeWindow(stockHistory, asOfIdx, forwardSessions, baseLow, bias, indexHistory, settings);

    private static CriterionForwardOutcome MeasureOutcomeT25(
        IReadOnlyList<OhlcvBar> stockHistory,
        int asOfIdx,
        decimal baseLow,
        PatternBias bias,
        IReadOnlyList<OhlcvBar> indexHistory,
        CriterionAccuracySettings settings)
    {
        var startClose = stockHistory[asOfIdx].Close;
        if (startClose <= 0)
            return new(0, 0, 0, false, 0, false);

        var entryDate = stockHistory[asOfIdx].Date;
        var forwardPrice = TradingSessionMath.GetForwardPriceT25(stockHistory, entryDate);
        if (forwardPrice is null)
            return new(0, 0, 0, false, 0, false);

        var forwardChange = TradingSessionMath.GetForwardReturnPercent(startClose, forwardPrice) ?? 0;

        var window = stockHistory
            .Skip(asOfIdx + 1)
            .Take(T25MfeWindowSessions)
            .ToList();

        var (maxFavorable, maxAdverse, invalidated) = ScanWindow(window, startClose, baseLow, settings);
        var indexForward = ComputeIndexForwardChangeT25(indexHistory, entryDate);
        var rsForward = Math.Round(forwardChange - indexForward, 2);

        var isHit = bias switch
        {
            PatternBias.Bullish => IsBullishHit(
                forwardChange, maxFavorable, maxAdverse, invalidated, rsForward, settings),
            PatternBias.Bearish => IsBearishHit(
                forwardChange, maxFavorable, maxAdverse, invalidated, rsForward, settings),
            _ => false,
        };

        return new(forwardChange, maxFavorable, maxAdverse, invalidated, rsForward, isHit);
    }

    private static CriterionForwardOutcome MeasureOutcomeWindow(
        IReadOnlyList<OhlcvBar> stockHistory,
        int asOfIdx,
        int forwardSessions,
        decimal baseLow,
        PatternBias bias,
        IReadOnlyList<OhlcvBar> indexHistory,
        CriterionAccuracySettings settings)
    {
        var startClose = stockHistory[asOfIdx].Close;
        if (startClose <= 0)
            return new(0, 0, 0, false, 0, false);

        var window = stockHistory
            .Skip(asOfIdx + 1)
            .Take(forwardSessions)
            .ToList();

        if (window.Count == 0)
            return new(0, 0, 0, false, 0, false);

        var (maxFavorable, maxAdverse, invalidated) = ScanWindow(window, startClose, baseLow, settings);
        var endClose = window[^1].Close;
        var forwardChange = Math.Round((endClose - startClose) / startClose * 100m, 2);

        var indexForward = ComputeIndexForwardChange(indexHistory, stockHistory[asOfIdx].Date, forwardSessions);
        var rsForward = Math.Round(forwardChange - indexForward, 2);

        var isHit = bias switch
        {
            PatternBias.Bullish => IsBullishHit(
                forwardChange, maxFavorable, maxAdverse, invalidated, rsForward, settings),
            PatternBias.Bearish => IsBearishHit(
                forwardChange, maxFavorable, maxAdverse, invalidated, rsForward, settings),
            _ => false,
        };

        return new(forwardChange, maxFavorable, maxAdverse, invalidated, rsForward, isHit);
    }

    private static (decimal MaxFavorable, decimal MaxAdverse, bool Invalidated) ScanWindow(
        IReadOnlyList<OhlcvBar> window,
        decimal startClose,
        decimal baseLow,
        CriterionAccuracySettings settings)
    {
        decimal maxFavorable = 0;
        decimal maxAdverse = 0;
        var invalidated = false;

        foreach (var bar in window)
        {
            if (settings.RequireBaseIntact && bar.Low < baseLow)
                invalidated = true;

            var highPct = (bar.High - startClose) / startClose * 100m;
            var lowPct = (bar.Low - startClose) / startClose * 100m;
            maxFavorable = Math.Max(maxFavorable, highPct);
            maxAdverse = Math.Min(maxAdverse, lowPct);
        }

        return (Math.Round(maxFavorable, 2), Math.Round(maxAdverse, 2), invalidated);
    }

    public MarketWyckoffPhase ClassifyMarketPhase(IReadOnlyList<OhlcvBar> indexHistory, int asOfIdx)
    {
        if (indexHistory.Count < 2 || asOfIdx < 1)
            return MarketWyckoffPhase.Neutral;

        var slice = indexHistory.Take(asOfIdx + 1).ToList();
        var change5d = ComputeChangePercent(slice, 5);
        var close = slice[^1].Close;
        var maWindow = Math.Min(20, slice.Count);
        var ma20 = slice.Skip(slice.Count - maWindow).Average(b => b.Close);
        var aboveMa = close > ma20;

        if (change5d >= 1m && aboveMa)
            return MarketWyckoffPhase.Favorable;
        if (change5d <= -1.5m || (change5d < 0 && !aboveMa))
            return MarketWyckoffPhase.Unfavorable;
        return MarketWyckoffPhase.Neutral;
    }

    public string GetScoreBucket(int score) =>
        score >= 80 ? "80+" : score >= 70 ? "70-79" : "60-69";

    private static bool IsBullishHit(
        decimal forwardChange,
        decimal maxFavorable,
        decimal maxAdverse,
        bool invalidated,
        decimal rsForward,
        CriterionAccuracySettings settings)
    {
        if (settings.RequireBaseIntact && invalidated)
            return false;

        if (settings.RequireRelativeStrength && rsForward < 0)
            return false;

        if (maxFavorable < settings.SwingTargetPercent)
            return false;

        return forwardChange > settings.DirectionThresholdPercent;
    }

    private static bool IsBearishHit(
        decimal forwardChange,
        decimal maxFavorable,
        decimal maxAdverse,
        bool invalidated,
        decimal rsForward,
        CriterionAccuracySettings settings)
    {
        if (settings.RequireRelativeStrength && rsForward > 0)
            return false;

        if (-maxAdverse < settings.SwingTargetPercent)
            return false;

        return forwardChange < -settings.DirectionThresholdPercent;
    }

    private static decimal ComputeIndexForwardChangeT25(
        IReadOnlyList<OhlcvBar> indexHistory,
        DateOnly asOfDate)
    {
        decimal? startClose = null;
        for (var i = 0; i < indexHistory.Count; i++)
        {
            if (indexHistory[i].Date != asOfDate)
                continue;
            startClose = indexHistory[i].Close;
            break;
        }

        if (startClose is null or <= 0)
            return 0;

        var forward = TradingSessionMath.GetForwardPriceT25(indexHistory, asOfDate);
        return TradingSessionMath.GetForwardReturnPercent(startClose.Value, forward) ?? 0;
    }

    private static decimal ComputeIndexForwardChange(
        IReadOnlyList<OhlcvBar> indexHistory,
        DateOnly asOfDate,
        int forwardSessions)
    {
        var startIdx = -1;
        for (var i = 0; i < indexHistory.Count; i++)
        {
            if (indexHistory[i].Date == asOfDate)
            {
                startIdx = i;
                break;
            }
        }

        if (startIdx < 0 || startIdx + forwardSessions >= indexHistory.Count)
            return 0;

        var start = indexHistory[startIdx].Close;
        var end = indexHistory[startIdx + forwardSessions].Close;
        if (start <= 0)
            return 0;

        return Math.Round((end - start) / start * 100m, 2);
    }

    private static decimal ComputeChangePercent(IReadOnlyList<OhlcvBar> history, int days)
    {
        if (history.Count < days + 1)
            return 0;

        var latest = history[^1].Close;
        var previous = history[^(days + 1)].Close;
        if (previous <= 0)
            return 0;

        return Math.Round((latest - previous) / previous * 100m, 2);
    }
}
