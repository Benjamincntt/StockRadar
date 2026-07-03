using StockRadar.Domain.Entities;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

/// <summary>
/// Phát hiện breakout hợp lệ từ hộp Darvas: nền kết thúc phiên trước, nến cuối là phiên kích hoạt.
/// </summary>
public sealed class DarvasBreakoutAnalyzer
{
    public DarvasBreakoutResult Evaluate(
        IReadOnlyList<OhlcvBar> history,
        BasePriceFilterSettings filter)
    {
        var darvas = filter.Darvas ?? DarvasBoxSettings.Default;
        return Evaluate(history, darvas, filter.ConsolidationMinSessions, filter.MaxBaseWindowSessions);
    }

    public DarvasBreakoutResult Evaluate(
        IReadOnlyList<OhlcvBar> history,
        DarvasBoxSettings darvasConfig,
        int minSessions = 10,
        int maxSessions = 45)
    {
        if (history.Count < minSessions + 1)
            return DarvasBreakoutResult.Invalid;

        var current = history[^1];
        var prior = history[^2];
        var baseHistoryEnd = history.Count - 2;

        var boxStart = -1;
        var boxEnd = -1;

        for (var len = maxSessions; len >= minSessions; len--)
        {
            if (baseHistoryEnd + 1 < len)
                continue;

            var start = baseHistoryEnd - len + 1;
            var end = baseHistoryEnd;

            if (BaseQualityEvaluator.PassesDarvasBox(
                    history,
                    start,
                    end,
                    darvasConfig,
                    requireVolumeDryUp: false,
                    maxBoxHeightPercent: darvasConfig.BreakoutMaxBoxHeightPercent))
            {
                boxStart = start;
                boxEnd = end;
                break;
            }
        }

        if (boxStart < 0)
            return DarvasBreakoutResult.Invalid;

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

        if (current.Close <= boxMaxClose)
            return DarvasBreakoutResult.Invalid;

        if (prior.Close <= 0)
            return DarvasBreakoutResult.Invalid;

        var priceGainPercent = (current.Close - prior.Close) / prior.Close * 100m;
        if (priceGainPercent < darvasConfig.BreakoutMinPriceGainPercent)
            return DarvasBreakoutResult.Invalid;

        if (boxAvgVolume <= 0)
            return DarvasBreakoutResult.Invalid;

        var volMultiplier = current.Volume / boxAvgVolume;
        if (volMultiplier < darvasConfig.BreakoutMinVolumeMultiplier)
            return DarvasBreakoutResult.Invalid;

        var totalRange = current.High - current.Low;
        if (totalRange > 0)
        {
            var upperShadowRatio = (current.High - current.Close) / totalRange;
            if (upperShadowRatio > darvasConfig.BreakoutMaxUpperShadowRatio)
                return DarvasBreakoutResult.Invalid;
        }

        var refPeriod =
            $"{history[boxStart].Date:dd/MM} → {history[boxEnd].Date:dd/MM} ({boxLen} phiên)";

        return new DarvasBreakoutResult(
            true,
            Math.Round(priceGainPercent, 2),
            Math.Round(volMultiplier, 2),
            current.Close,
            boxMinClose,
            refPeriod,
            boxMaxClose,
            boxMinClose,
            boxLen);
    }
}
