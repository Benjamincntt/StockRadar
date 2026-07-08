using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Enums;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Infrastructure.MarketData;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Infrastructure.Notifications;

internal static class TopOpportunityVipAlertEvaluator
{
    public const string EntryReadySignal = "EntryReady";

    public static bool IsPriceInEntryZone(EntryPointDto entry, decimal livePrice, decimal tolerancePercent = 0.15m)
    {
        if (!string.Equals(entry.Status, nameof(EntryPointStatus.Ready), StringComparison.Ordinal)
            && !string.Equals(entry.Status, nameof(EntryPointStatus.Watch), StringComparison.Ordinal))
        {
            return false;
        }

        var low = Math.Min(entry.BaseLow, entry.EntryPrice);
        if (entry.StopLoss > 0)
            low = Math.Min(low, entry.StopLoss);

        var high = Math.Max(entry.EntryPrice, entry.TriggerPrice);
        if (high <= 0 || low <= 0)
            return false;

        if (high < low)
            (low, high) = (high, low);

        var tolerance = livePrice * tolerancePercent / 100m;
        return livePrice >= low - tolerance && livePrice <= high + tolerance;
    }

    public static decimal GainFromBasePeakPercent(EntryPointDto? entry, decimal livePrice)
    {
        var peak = entry?.BaseHigh ?? 0m;
        if (peak <= 0 || livePrice <= 0)
            return 0m;

        return Math.Round((livePrice - peak) / peak * 100m, 1);
    }

    public static decimal ComputePacedVolumeRatio(
        long sessionVolume,
        long avgDailyVolume,
        decimal sessionElapsedFraction)
    {
        if (avgDailyVolume <= 0 || sessionElapsedFraction <= 0.01m)
            return 0m;

        var projected = sessionVolume / sessionElapsedFraction;
        return Math.Round(projected / avgDailyVolume, 2);
    }

    public static string? EvaluateMasterSignal(
        MasterAlertOptions cfg,
        MasterAlertSessionTracker.SymbolMasterState state,
        EntryPointDto? entry,
        KbsPriceBoardClient.KbsBoardRow row,
        TradeEventDetector.DetectedTradeEvent? scan,
        decimal pacedVolumeRatio,
        long avgDailyVolume)
    {
        if (row.Close <= 0)
            return null;

        var gainFromBase = GainFromBasePeakPercent(entry, row.Close);
        if (gainFromBase <= 0)
            return null;

        if (!state.BuyPoint1Fired
            && IsInBuyPoint1Band(gainFromBase, cfg)
            && PassesVolumeGate(cfg, row.SessionVolume, pacedVolumeRatio, avgDailyVolume))
        {
            state.BuyPoint1Fired = true;
            state.BuyPoint1Price = row.Close;
            state.SessionHighSinceBuy1 = Math.Max(row.High, row.Close);
            return MasterAlertKinds.BuyPoint1;
        }

        state.UpdateHigh(row.High);

        if (!state.BuyPoint2Fired
            && gainFromBase >= cfg.BuyPoint2MinChangePercent
            && PassesVolumeGate(cfg, row.SessionVolume, pacedVolumeRatio, avgDailyVolume))
        {
            if (!state.BuyPoint1Fired)
            {
                state.BuyPoint1Fired = true;
                state.BuyPoint1Price = row.Close;
                state.SessionHighSinceBuy1 = Math.Max(row.High, row.Close);
            }

            state.BuyPoint2Fired = true;
            return MasterAlertKinds.BuyPoint2;
        }

        if (!state.BuyPoint1Fired)
            return null;

        var peak = state.PeakGainPercent();
        if (!IsDistributionScan(scan))
            return null;

        if (!state.CutLoss1Fired && peak >= cfg.CutLoss1MinPeakGainPercent)
        {
            state.CutLoss1Fired = true;
            return MasterAlertKinds.CutLoss1;
        }

        if (!state.CutAllFired && peak >= cfg.CutAllMinPeakGainPercent)
        {
            state.CutAllFired = true;
            return MasterAlertKinds.CutAll;
        }

        return null;
    }

    private static bool IsInBuyPoint1Band(decimal gainFromBase, MasterAlertOptions cfg) =>
        gainFromBase >= cfg.BuyPoint1MinChangePercent
        && gainFromBase <= cfg.BuyPoint1MaxChangePercent;

    private static bool PassesVolumeGate(
        MasterAlertOptions cfg,
        long sessionVolume,
        decimal pacedVolumeRatio,
        long avgDailyVolume)
    {
        if (avgDailyVolume > 0)
        {
            return (cfg.MinSessionVolumeFloor <= 0 || sessionVolume >= cfg.MinSessionVolumeFloor)
                && pacedVolumeRatio >= cfg.MinVolumeRatioPaced;
        }

        return sessionVolume >= cfg.MinSessionVolume;
    }

    private static bool IsDistributionScan(TradeEventDetector.DetectedTradeEvent? scan)
    {
        if (scan is null || !scan.IsImmediateBlock)
            return false;

        if (string.Equals(scan.Label, TradeEventLabels.Xa, StringComparison.Ordinal))
            return true;

        return scan.ForeignNetDelta < 0 && scan.PropDelta <= 0;
    }

    public static SignalType SignalTypeFor(string signalKey) => signalKey switch
    {
        MasterAlertKinds.BuyPoint1 or MasterAlertKinds.BuyPoint2 => SignalType.Breakout,
        MasterAlertKinds.CutLoss1 or MasterAlertKinds.CutAll => SignalType.Distribution,
        EntryReadySignal => SignalType.Shakeout,
        _ => SignalType.Breakout,
    };

    public static AlertCategory CategoryFor(string signalKey) =>
        MasterAlertKinds.IsSellKind(signalKey) ? AlertCategory.Sell : AlertCategory.Buy;
}
