using StockRadar.Domain.Entities;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

/// <summary>Hộp tích lũy phẳng Darvas + xác nhận phá vỡ (thay pipeline nền giá cũ).</summary>
public sealed class DarvasBreakoutAnalyzer
{
    public FlatBoxProfile Analyze(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter)
    {
        var darvas = filter.Darvas ?? DarvasBoxSettings.Default;
        return Analyze(history, darvas, filter.ConsolidationMinSessions, filter.MaxBaseWindowSessions);
    }

    public FlatBoxProfile Analyze(
        IReadOnlyList<OhlcvBar> history,
        DarvasBoxSettings darvasConfig,
        int minSessions = 10,
        int maxSessions = 45)
    {
        if (history.Count < minSessions + 1)
            return FlatBoxProfile.None;

        if (!TryFindBoxWindow(history, history.Count - 2, minSessions, maxSessions, darvasConfig, out var boxStart, out var boxEnd))
            return FlatBoxProfile.None;

        var boxMaxClose = decimal.MinValue;
        var boxMinClose = decimal.MaxValue;
        var volSum = 0m;
        var boxLen = boxEnd - boxStart + 1;
        for (var i = boxStart; i <= boxEnd; i++)
        {
            boxMaxClose = Math.Max(boxMaxClose, history[i].Close);
            boxMinClose = Math.Min(boxMinClose, history[i].Close);
            volSum += history[i].Volume;
        }

        var boxAvgVolume = volSum / boxLen;
        var current = history[^1];
        var prior = history[^2];
        var gainFromTop = boxMaxClose <= 0
            ? 0
            : Math.Round((current.Close - boxMaxClose) / boxMaxClose * 100m, 2);

        var refPeriod =
            $"{history[boxStart].Date:dd/MM} → {history[boxEnd].Date:dd/MM} ({boxLen} phiên)";

        var isBreakout = PassesBreakoutGates(
            current, prior, boxMaxClose, boxAvgVolume, darvasConfig, out var priceGain, out var volMult);

        return new FlatBoxProfile(
            true,
            isBreakout,
            boxMinClose,
            boxMaxClose,
            boxLen,
            history[boxStart].Date,
            history[boxEnd].Date,
            gainFromTop,
            boxMinClose,
            isBreakout ? priceGain : null,
            isBreakout ? volMult : null,
            refPeriod);
    }

    public DarvasBreakoutResult Evaluate(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter)
    {
        var box = Analyze(history, filter);
        if (!box.IsBreakoutConfirmed)
            return DarvasBreakoutResult.Invalid;

        return new DarvasBreakoutResult(
            true,
            box.PriceGainPercent!.Value,
            box.VolumeMultiplier!.Value,
            history[^1].Close,
            box.SuggestedStopLoss,
            box.RefBoxPeriod,
            box.BoxHigh,
            box.BoxLow,
            box.SessionDays);
    }

    private static bool TryFindBoxWindow(
        IReadOnlyList<OhlcvBar> history,
        int boxEnd,
        int minSessions,
        int maxSessions,
        DarvasBoxSettings darvasConfig,
        out int boxStart,
        out int end)
    {
        boxStart = -1;
        end = boxEnd;

        for (var len = maxSessions; len >= minSessions; len--)
        {
            if (boxEnd + 1 < len)
                continue;

            var start = boxEnd - len + 1;
            if (BaseQualityEvaluator.PassesDarvasBox(
                    history,
                    start,
                    boxEnd,
                    darvasConfig,
                    requireVolumeDryUp: false,
                    maxBoxHeightPercent: darvasConfig.BreakoutMaxBoxHeightPercent))
            {
                boxStart = start;
                return true;
            }
        }

        return false;
    }

    private static bool PassesBreakoutGates(
        OhlcvBar current,
        OhlcvBar prior,
        decimal boxMaxClose,
        decimal boxAvgVolume,
        DarvasBoxSettings cfg,
        out decimal priceGainPercent,
        out decimal volMultiplier)
    {
        priceGainPercent = 0;
        volMultiplier = 0;

        if (current.Close <= boxMaxClose)
            return false;

        if (prior.Close <= 0)
            return false;

        priceGainPercent = Math.Round((current.Close - prior.Close) / prior.Close * 100m, 2);
        if (priceGainPercent < cfg.BreakoutMinPriceGainPercent)
            return false;

        if (boxAvgVolume <= 0)
            return false;

        volMultiplier = Math.Round(current.Volume / boxAvgVolume, 2);
        if (volMultiplier < cfg.BreakoutMinVolumeMultiplier)
            return false;

        var totalRange = current.High - current.Low;
        if (totalRange > 0)
        {
            var upperShadowRatio = (current.High - current.Close) / totalRange;
            if (upperShadowRatio > cfg.BreakoutMaxUpperShadowRatio)
                return false;
        }

        return true;
    }
}
