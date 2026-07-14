using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Enums;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.MarketData;

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
        decimal sessionElapsedFraction,
        decimal minElapsedFraction = 0.2m)
    {
        if (avgDailyVolume <= 0 || sessionElapsedFraction <= 0.01m)
            return 0m;

        var effective = Math.Max(sessionElapsedFraction, minElapsedFraction);
        var projected = sessionVolume / effective;
        return Math.Round(projected / avgDailyVolume, 2);
    }

    /// <summary>Chỉ đánh giá tín hiệu MUA. Bán/cảnh báo → <see cref="EvaluatePositionSignal"/>.</summary>
    public static string? EvaluateMasterSignal(
        MasterAlertOptions cfg,
        MasterAlertSessionTracker.SymbolMasterState state,
        EntryPointDto? entry,
        KbsPriceBoardClient.KbsBoardRow row,
        TradeEventDetector.DetectedTradeEvent? scan,
        decimal pacedVolumeRatio,
        long avgDailyVolume,
        string marketPhase)
    {
        _ = scan;
        _ = marketPhase;

        if (row.Close <= 0)
            return null;

        var gainFromBase = GainFromBasePeakPercent(entry, row.Close);
        if (gainFromBase <= 0)
            return null;

        if (!state.BuyPoint1Fired)
        {
            if (IsInBuyPoint1Band(gainFromBase, cfg))
            {
                state.BuyPoint1ConfirmTicks++;

                if (state.BuyPoint1ConfirmTicks >= cfg.RequiredConfirmationTicks
                    && PassesVolumeGate(
                        cfg, row.SessionVolume, pacedVolumeRatio, avgDailyVolume, cfg.MinVolumeRatioPaced))
                {
                    state.BuyPoint1Fired = true;
                    state.BuyPoint1Price = row.Close;
                    state.SessionHighSinceBuy1 = Math.Max(row.High, row.Close);
                    return MasterAlertKinds.BuyPoint1;
                }
            }
            else if (gainFromBase < cfg.BuyPoint1MinChangePercent)
            {
                state.BuyPoint1ConfirmTicks = 0;
            }
        }

        state.UpdateHigh(row.High);

        if (!state.BuyPoint2Fired)
        {
            if (gainFromBase >= cfg.BuyPoint2MinChangePercent)
            {
                state.BuyPoint2ConfirmTicks++;

                if (state.BuyPoint2ConfirmTicks >= cfg.RequiredConfirmationTicks
                    && PassesVolumeGate(
                        cfg, row.SessionVolume, pacedVolumeRatio, avgDailyVolume, cfg.BuyPoint2MinVolumeRatio))
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
            }
            else
            {
                state.BuyPoint2ConfirmTicks = 0;
            }
        }

        return null;
    }

    public static string? EvaluatePositionSignal(
        MasterAlertOptions cfg,
        MasterAlertPositionRecord position,
        KbsPriceBoardClient.KbsBoardRow row,
        TradeEventDetector.DetectedTradeEvent? scan,
        DateOnly currentSessionDate,
        string marketPhase)
    {
        if (row.Close <= 0 || position.EntryPrice <= 0)
            return null;

        var peakPrice = Math.Max(position.PeakPriceSinceEntry, row.High);
        var peakGain = (peakPrice - position.EntryPrice) / position.EntryPrice * 100m;
        var currentGain = (row.Close - position.EntryPrice) / position.EntryPrice * 100m;
        var drawdown = Math.Max(0m, peakGain - currentGain);

        var sessions = TradingSessionMath.TradingSessionsBetween(position.EntryDate, currentSessionDate);
        var canSell = sessions >= cfg.MinTradingSessionsToSell;

        if (!canSell)
        {
            if (position.FiredAlertKinds.Contains(MasterAlertKinds.RiskWarningIntraday, StringComparer.Ordinal))
                return null;

            var severe = drawdown >= cfg.RiskWarningDrawdownFromPeakPercent;
            if (IsDistributionScan(scan) || severe)
                return MasterAlertKinds.RiskWarningIntraday;

            return null;
        }

        if (!cfg.MarketPhaseMultipliers.TryGetValue(marketPhase, out var mult))
            mult = 1.0m;

        var stop1 = cfg.BaseTrailingStopPercent1 * mult;
        var stop2 = cfg.BaseTrailingStopPercent2 * mult;
        var soldHalf = position.FiredAlertKinds.Contains(MasterAlertKinds.SellPoint1Half, StringComparer.Ordinal);

        if (peakGain >= cfg.TrailingStopMinPeak)
        {
            if (drawdown >= stop2)
                return MasterAlertKinds.SellAll;

            if (!soldHalf && drawdown >= stop1)
                return MasterAlertKinds.SellPoint1Half;
        }

        if (IsDistributionScan(scan))
        {
            if (peakGain >= cfg.CutAllMinPeakGainPercent)
                return MasterAlertKinds.SellAll;

            if (!soldHalf && peakGain >= cfg.CutLoss1MinPeakGainPercent)
                return MasterAlertKinds.SellPoint1Half;
        }

        return null;
    }

    private static bool IsInBuyPoint1Band(decimal gainFromBase, MasterAlertOptions cfg) =>
        gainFromBase >= cfg.BuyPoint1MinChangePercent
        && gainFromBase < cfg.BuyPoint2MinChangePercent;

    private static bool PassesVolumeGate(
        MasterAlertOptions cfg,
        long sessionVolume,
        decimal pacedVolumeRatio,
        long avgDailyVolume,
        decimal minVolumeRatio)
    {
        if (avgDailyVolume > 0)
        {
            return (cfg.MinSessionVolumeFloor <= 0 || sessionVolume >= cfg.MinSessionVolumeFloor)
                && pacedVolumeRatio >= minVolumeRatio;
        }

        return sessionVolume >= cfg.MinSessionVolume;
    }

    internal static bool IsDistributionScan(TradeEventDetector.DetectedTradeEvent? scan)
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
        MasterAlertKinds.CutLoss1 or MasterAlertKinds.CutAll
            or MasterAlertKinds.SellPoint1Half or MasterAlertKinds.SellAll
            or MasterAlertKinds.RiskWarningIntraday => SignalType.Distribution,
        EntryReadySignal => SignalType.Shakeout,
        _ => SignalType.Breakout,
    };

    public static AlertCategory CategoryFor(string signalKey) =>
        MasterAlertKinds.IsSellKind(signalKey) || MasterAlertKinds.IsRiskWarning(signalKey)
            ? AlertCategory.Sell
            : AlertCategory.Buy;
}
