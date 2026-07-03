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
    public bool HasValidTrendSetup(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings runup,
        SmartMoneySettings smartMoney)
    {
        var baseProfile = signals.AnalyzeBasePriceForFilter(history, runup);
        if (baseProfile is null)
            return false;

        if (baseProfile.GainFromBasePercent > runup.MaxGainFromBasePercent)
            return false;

        if (!signals.MeetsSessionEntryBar(
                history,
                smartMoney.MinSessionChangePercent,
                smartMoney.MinSessionVolume))
            return false;

        var stock = new Stock("", "", "", history);
        var detected = signals.DetectSignals(stock, 0m, runup);
        return detected.Contains(SignalType.Breakout)
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

        decimal maxFavorable = 0;
        decimal maxAdverse = 0;
        var invalidated = false;

        foreach (var bar in window)
        {
            if (bar.Low < baseLow)
                invalidated = true;

            var highPct = (bar.High - startClose) / startClose * 100m;
            var lowPct = (bar.Low - startClose) / startClose * 100m;
            maxFavorable = Math.Max(maxFavorable, highPct);
            maxAdverse = Math.Min(maxAdverse, lowPct);
        }

        var endClose = window[^1].Close;
        var forwardChange = Math.Round((endClose - startClose) / startClose * 100m, 2);
        maxFavorable = Math.Round(maxFavorable, 2);
        maxAdverse = Math.Round(maxAdverse, 2);

        var indexForward = ComputeIndexForwardChange(indexHistory, stockHistory[asOfIdx].Date, forwardSessions);
        var rsForward = Math.Round(forwardChange - indexForward, 2);

        var isHit = bias switch
        {
            PatternBias.Bullish => IsBullishHit(
                forwardChange,
                maxFavorable,
                maxAdverse,
                invalidated,
                rsForward,
                settings),
            PatternBias.Bearish => IsBearishHit(
                forwardChange,
                maxFavorable,
                maxAdverse,
                invalidated,
                rsForward,
                settings),
            _ => false,
        };

        return new(
            forwardChange,
            maxFavorable,
            maxAdverse,
            invalidated,
            rsForward,
            isHit);
    }

    public MarketWyckoffPhase ClassifyMarketPhase(IReadOnlyList<OhlcvBar> indexHistory, int asOfIdx)
    {
        if (indexHistory.Count < 2 || asOfIdx < 1)
            return MarketWyckoffPhase.Neutral;

        // Phân pha theo xu hướng 5 phiên + vị trí so với MA20 — biến động 1 ngày quá nhiễu
        // (ngưỡng cũ ±0.5%/ngày khiến hầu hết phiên bị xếp Neutral).
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
