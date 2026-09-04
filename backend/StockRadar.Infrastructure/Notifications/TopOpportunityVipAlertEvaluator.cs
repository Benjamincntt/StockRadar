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
    public const string BuyTriggerBreakout = "breakout";
    public const string BuyTriggerPullback = "pullback";
    public const string BuyTriggerScaleIn = "scale_in";
    public const string BuyTriggerDipBounce = "dip_bounce";

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

    public static decimal GainFromOpenPercent(decimal open, decimal livePrice)
    {
        if (open <= 0 || livePrice <= 0)
            return 0m;

        return Math.Round((livePrice - open) / open * 100m, 1);
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
        string marketPhase,
        VipPullbackMaContext? pullbackMa,
        decimal mlProb,
        bool mlModelActive,
        bool featuresComplete,
        decimal resolvedMinMlProb,
        long? foreignNet,
        bool orderflowObserved,
        bool indexNearPriorPeak,
        bool trapContextActive,
        bool liveIndexAbovePin,
        bool pastAfternoonCheckpoint,
        bool pinWindowIntegrityHeld,
        long? foreignNetSinceAfternoon,
        out string? buyTriggerBranch,
        out bool blockedByMl,
        out bool blockedByAntiSpam,
        out bool blockedByBullTrap,
        out bool deferredByCheckpoint)
    {
        _ = scan;
        buyTriggerBranch = null;
        blockedByMl = false;
        blockedByAntiSpam = false;
        blockedByBullTrap = false;
        deferredByCheckpoint = false;

        if (row.Close <= 0 || row.Open <= 0)
            return null;

        // Window-integrity tracking (slice 2, opt-in): ghi nhận ngay khi Close rời high phiên từ
        // 13:00, KHÔNG phụ thuộc guard actionable/entry — phải quan sát suốt cửa sổ, không chỉ lúc
        // đủ điều kiện bắn. Một khi thủng, không tự phục hồi trong phiên (đọc ở IsCloseNearSessionHigh).
        if (row.High > 0
            && VietnamMarketCalendar.NowVietnam().TimeOfDay >= new TimeSpan(13, 0, 0)
            && !IsCloseNearSessionHigh(row, cfg))
        {
            state.AfternoonShapeIntegrityBroken = true;
        }

        // Guard: chỉ bắn Master Buy khi đủ điều kiện SmartMoney (IsActionable)
        if (entry?.IsActionable != true)
            return null;

        var isBullTrapEnv = IsBullTrapEnvironment(cfg, indexNearPriorPeak, marketPhase);
        var gainFromOpen = GainFromOpenPercent(row.Open, row.Close);
        var breakoutBand = IsInBuyPoint1Band(gainFromOpen, cfg);
        var breakoutStrong = gainFromOpen >= cfg.BuyPoint2MinChangePercent;
        var pullbackEligible = IsPullbackBuy1Eligible(cfg, pullbackMa, row.Close, gainFromOpen);
        var dipBounceEligible = IsDipBounceBuy1Eligible(cfg, pullbackMa, row, gainFromOpen);
        var vsaLabel = scan?.Label;

        // Deferral (slice 1): trong trap-zone (env bật HOẶC đã xuyên đỉnh đã ghim), hoãn Buy tới
        // checkpoint chiều rồi mới bắn nếu shape còn giữ. Hai trigger ở hai trạng thái env ngược
        // nhau nên KHÔNG treo lên isBullTrapEnv từng vòng — vế xuyên dùng pin bền.
        var trapDeferralActive = isBullTrapEnv || (trapContextActive && liveIndexAbovePin);
        var closeNearHigh = IsCloseNearSessionHigh(row, cfg);

        // Slice 2 (opt-in, mặc định tắt trừ hysteresis đã gói trong indexNearPriorPeak ở caller):
        // window-integrity siết closeNearHigh (mã) — chỉ đọc lại tại checkpoint không đủ, phải
        // chưa từng thủng suốt cửa sổ 13:00→checkpoint.
        var closeShapeOk = cfg.BullTrapDeferralRequireWindowIntegrity
            ? closeNearHigh && !state.AfternoonShapeIntegrityBroken
            : closeNearHigh;

        // Predicate riêng cho vế xuyên đỉnh (index vs pin) — chỉ có ý nghĩa khi đang xuyên
        // (liveIndexAbovePin); nếu không đang xuyên, cờ này không phải mối lo, để true.
        var indexShapeOk = !liveIndexAbovePin
            || !cfg.BullTrapDeferralRequireWindowIntegrity
            || pinWindowIntegrityHeld;

        // Foreign-hold (slice 2, opt-in): khối ngoại chưa quay đầu bán từ 13:00. Thiếu dữ liệu
        // (null = chưa qua 13:00 hoặc không có orderflow) → fail-open, không chặn thêm.
        var foreignHoldOk = !cfg.BullTrapDeferralRequireForeignHold
            || foreignNetSinceAfternoon is null
            || foreignNetSinceAfternoon >= 0;

        var shapeConfirmed = closeShapeOk && indexShapeOk && foreignHoldOk;
        var deferralBlocks = cfg.BullTrapDeferralEnabled
            && trapDeferralActive
            && (!pastAfternoonCheckpoint || !shapeConfirmed);

        if (!state.BuyPoint1Fired)
        {
            string? pendingBranch = null;
            var buy1Eligible = false;
            if (isBullTrapEnv)
            {
                if (dipBounceEligible)
                {
                    buy1Eligible = true;
                    pendingBranch = BuyTriggerDipBounce;
                }
                else if (breakoutBand || pullbackEligible)
                {
                    if (cfg.BullTrapSoftBlockEnabled)
                    {
                        // Softblock: không hardblock, đẩy vào deferral (trapDeferralActive = true).
                        // Breakout/pullback chỉ bắn nếu qua checkpoint chiều và shape giữ.
                        buy1Eligible = true;
                        pendingBranch = pullbackEligible && !breakoutBand
                            ? BuyTriggerPullback
                            : BuyTriggerBreakout;
                    }
                    else
                    {
                        blockedByBullTrap = true;
                    }
                }
                else if (breakoutStrong)
                {
                    // breakoutStrong (≥Buy2) không phải Buy1 territory — log thôi.
                    blockedByBullTrap = true;
                }
            }
            else
            {
                buy1Eligible = breakoutBand || pullbackEligible;
                if (buy1Eligible)
                {
                    pendingBranch = pullbackEligible && !breakoutBand
                        ? BuyTriggerPullback
                        : BuyTriggerBreakout;
                }
            }

            if (buy1Eligible && deferralBlocks)
                deferredByCheckpoint = true;

            if (buy1Eligible && !deferralBlocks)
            {
                state.BuyPoint1ConfirmTicks++;

                if (state.BuyPoint1ConfirmTicks >= cfg.RequiredConfirmationTicks
                    && PassesVolumeGate(
                        cfg, row.SessionVolume, pacedVolumeRatio, avgDailyVolume, cfg.MinVolumeRatioPaced))
                {
                    if (ShouldBlockByMl(
                            cfg, mlProb, mlModelActive, featuresComplete, marketPhase, resolvedMinMlProb))
                    {
                        blockedByMl = true;
                        return null;
                    }

                    if (ShouldBlockByAntiSpam(
                            cfg, mlProb, resolvedMinMlProb, foreignNet, vsaLabel, orderflowObserved))
                    {
                        blockedByAntiSpam = true;
                        return null;
                    }

                    state.BuyPoint1Fired = true;
                    state.BuyPoint1Price = row.Close;
                    state.SessionHighSinceBuy1 = Math.Max(row.High, row.Close);
                    buyTriggerBranch = pendingBranch ?? BuyTriggerBreakout;
                    return MasterAlertKinds.BuyPoint1;
                }
            }
            else
            {
                state.BuyPoint1ConfirmTicks = 0;
            }
        }

        state.UpdateHigh(row.High);

        if (!state.BuyPoint2Fired)
        {
            if (isBullTrapEnv)
            {
                // Scale-in: chỉ cần đã có Buy1 và lãi ≥ ngưỡng so entry — không vol/ticks/ML.
                if (state.BuyPoint1Fired
                    && state.BuyPoint1Price > 0
                    && GainFromEntryPercent(state.BuyPoint1Price, row.Close) >= cfg.BullTrapBuy2ScaleInGainPercent)
                {
                    state.BuyPoint2Fired = true;
                    buyTriggerBranch = BuyTriggerScaleIn;
                    return MasterAlertKinds.BuyPoint2;
                }
            }
            else if (breakoutStrong && deferralBlocks)
            {
                // Cú xuyên mạnh (≥Buy2 band) cũng phải chờ chiều — không cho bắn thẳng Buy2+Buy1.
                deferredByCheckpoint = true;
                state.BuyPoint2ConfirmTicks = 0;
            }
            else if (breakoutStrong)
            {
                state.BuyPoint2ConfirmTicks++;

                if (state.BuyPoint2ConfirmTicks >= cfg.RequiredConfirmationTicks
                    && PassesVolumeGate(
                        cfg, row.SessionVolume, pacedVolumeRatio, avgDailyVolume, cfg.BuyPoint2MinVolumeRatio))
                {
                    if (ShouldBlockByMl(
                            cfg, mlProb, mlModelActive, featuresComplete, marketPhase, resolvedMinMlProb))
                    {
                        blockedByMl = true;
                        return null;
                    }

                    if (ShouldBlockByAntiSpam(
                            cfg, mlProb, resolvedMinMlProb, foreignNet, vsaLabel, orderflowObserved))
                    {
                        blockedByAntiSpam = true;
                        return null;
                    }

                    if (!state.BuyPoint1Fired)
                    {
                        state.BuyPoint1Fired = true;
                        state.BuyPoint1Price = row.Close;
                        state.SessionHighSinceBuy1 = Math.Max(row.High, row.Close);
                    }

                    state.BuyPoint2Fired = true;
                    buyTriggerBranch = BuyTriggerBreakout;
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

    public static bool IsBullTrapEnvironment(
        MasterAlertOptions cfg,
        bool indexNearPriorPeak,
        string marketPhase) =>
        VnIndexPriorPeakAnalyzer.IsBullTrapEnvironment(
            cfg.BullTrapGateEnabled, indexNearPriorPeak, marketPhase, cfg.BullTrapBlockOnNeutral);

    /// <summary>Alias — env bull-trap (không còn nghĩa chặn mọi Buy).</summary>
    public static bool ShouldBlockByBullTrap(
        MasterAlertOptions cfg,
        bool indexNearPriorPeak,
        string marketPhase) =>
        IsBullTrapEnvironment(cfg, indexNearPriorPeak, marketPhase);

    public static decimal GainFromEntryPercent(decimal entryPrice, decimal livePrice)
    {
        if (entryPrice <= 0 || livePrice <= 0)
            return 0m;
        return Math.Round((livePrice - entryPrice) / entryPrice * 100m, 2);
    }

    /// <summary>
    /// Bull-trap Buy1: kênh trên (uptrend dài hạn) + đã rũ (≥N phiên đỏ) + phiên hiện tại xanh đầu.
    /// </summary>
    public static bool IsDipBounceBuy1Eligible(
        MasterAlertOptions cfg,
        VipPullbackMaContext? pullbackMa,
        KbsPriceBoardClient.KbsBoardRow row,
        decimal gainFromOpen)
    {
        if (pullbackMa is null || !pullbackMa.Available || !pullbackMa.UptrendLong)
            return false;

        if (!pullbackMa.HasRecentDip)
            return false;

        // Phiên xanh đầu: close > open hoặc đã dương so Open.
        if (row.Close <= row.Open && gainFromOpen <= 0)
            return false;

        return true;
    }

    /// <summary>
    /// Shape checkpoint (endpoint, slice 1): mã còn sát high phiên khi tới checkpoint chiều.
    /// (High−Close)/High ≤ ngưỡng → chưa trượt khỏi đỉnh phiên → chưa lộ xả.
    /// </summary>
    public static bool IsCloseNearSessionHigh(
        KbsPriceBoardClient.KbsBoardRow row,
        MasterAlertOptions cfg)
    {
        if (row.High <= 0 || row.Close <= 0)
            return false;

        var band = Math.Max(0m, cfg.BullTrapDeferralCloseWithinHighPercent);
        return (row.High - row.Close) / row.High * 100m <= band;
    }

    /// <summary>Fail-open: tắt gate / model inactive / feature thiếu → không chặn.</summary>
    public static bool ShouldBlockByMl(
        MasterAlertOptions cfg,
        decimal mlProb,
        bool mlModelActive,
        bool featuresComplete,
        string marketPhase,
        decimal? resolvedMinMlProb = null)
    {
        if (!cfg.MlGateEnabled || !mlModelActive || !featuresComplete)
            return false;

        var min = resolvedMinMlProb
            ?? (cfg.MinMlProbToFire.TryGetValue(marketPhase, out var m)
                ? m
                : cfg.MinMlProbToFire.TryGetValue("Neutral", out m) ? m : 52m);

        return mlProb < min;
    }

    /// <summary>
    /// Anti-spam vùng biên: P gần ngưỡng → cần foreignNet≥0 và không VSA xả.
    /// Thiếu orderflow → fail-open (không chặn thêm).
    /// </summary>
    public static bool ShouldBlockByAntiSpam(
        MasterAlertOptions cfg,
        decimal mlProb,
        decimal minMlProb,
        long? foreignNet,
        string? vsaLabel,
        bool orderflowObserved)
    {
        if (!orderflowObserved)
            return false;

        var band = Math.Max(0m, cfg.AntiSpamBorderBandPercent);
        if (mlProb < minMlProb || mlProb > minMlProb + band)
            return false;

        if (cfg.AntiSpamRequireNonNegativeForeign && foreignNet is < 0)
            return true;

        if (cfg.AntiSpamBlockVsaXa
            && string.Equals(vsaLabel, TradeEventLabels.Xa, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public static decimal ResolveMinMlProb(MasterAlertOptions cfg, string marketPhase)
    {
        if (cfg.MinMlProbToFire.TryGetValue(marketPhase, out var min))
            return min;
        return cfg.MinMlProbToFire.TryGetValue("Neutral", out min) ? min : 52m;
    }

    public static string? EvaluatePositionSignal(
        MasterAlertOptions cfg,
        MasterAlertPositionRecord position,
        KbsPriceBoardClient.KbsBoardRow row,
        TradeEventDetector.DetectedTradeEvent? scan,
        DateOnly currentSessionDate,
        string marketPhase,
        decimal anchorPrice)
    {
        if (row.Close <= 0 || position.EntryPrice <= 0)
            return null;

        var peakPrice = Math.Max(position.PeakPriceSinceEntry, row.High);
        var peakGain = (peakPrice - position.EntryPrice) / position.EntryPrice * 100m;
        var drawdownFromAnchor = anchorPrice > 0
            ? Math.Max(0m, (anchorPrice - row.Close) / anchorPrice * 100m)
            : 0m;

        var sessions = TradingSessionMath.TradingSessionsBetween(position.EntryDate, currentSessionDate);
        var canSell = sessions >= cfg.MinTradingSessionsToSell;
        var soldHalf = position.FiredAlertKinds.Contains(MasterAlertKinds.SellPoint1Half, StringComparer.Ordinal);
        var riskAlready = position.FiredAlertKinds.Contains(MasterAlertKinds.RiskWarningIntraday, StringComparer.Ordinal);

        if (!cfg.MarketPhaseMultipliers.TryGetValue(marketPhase, out var mult))
            mult = 1.0m;

        var stop1 = cfg.SellPoint1DropFromAnchorPercent * mult;
        var stop2 = cfg.SellPoint2DropFromAnchorPercent * mult;

        // Phủ nhận cây vượt đỉnh — ưu tiên cao nhất
        if (position.EntryBarLow is > 0 && row.Close < position.EntryBarLow.Value)
            return Emit(canSell, riskAlready, MasterAlertKinds.SellAll);

        string? candidate = null;

        if (MasterAlertExitRegimes.IsUnderBase(position.ExitRegime)
            && position.OverheadBaseLow is > 0)
        {
            var bufferPct = mult > 0 ? cfg.OverheadBaseBufferPercent / mult : cfg.OverheadBaseBufferPercent;
            var triggerHalf = position.OverheadBaseLow.Value * (1m - bufferPct / 100m);

            if (!soldHalf && row.Close >= triggerHalf)
                candidate = MasterAlertKinds.SellPoint1Half;
            else if (soldHalf && row.Close < position.OverheadBaseLow.Value)
                candidate = MasterAlertKinds.SellAll;
        }
        else
        {
            // BlueSky / mặc định
            if (drawdownFromAnchor >= stop2)
                candidate = MasterAlertKinds.SellAll;
            else if (!soldHalf && drawdownFromAnchor >= stop1)
                candidate = MasterAlertKinds.SellPoint1Half;
        }

        // Nhánh phân phối (phụ)
        if (candidate is null && IsDistributionScan(scan))
        {
            if (peakGain >= cfg.CutAllMinPeakGainPercent)
                candidate = MasterAlertKinds.SellAll;
            else if (!soldHalf && peakGain >= cfg.CutLoss1MinPeakGainPercent)
                candidate = MasterAlertKinds.SellPoint1Half;
        }

        if (candidate is not null)
            return Emit(canSell, riskAlready, candidate);

        if (!canSell)
        {
            if (riskAlready)
                return null;

            var severe = drawdownFromAnchor >= cfg.RiskWarningDrawdownFromPeakPercent;
            if (IsDistributionScan(scan) || severe)
                return MasterAlertKinds.RiskWarningIntraday;
        }

        return null;
    }

    private static string? Emit(bool canSell, bool riskAlready, string sellKind)
    {
        if (canSell)
            return sellKind;

        return riskAlready ? null : MasterAlertKinds.RiskWarningIntraday;
    }

    private static bool IsInBuyPoint1Band(decimal gainFromOpen, MasterAlertOptions cfg) =>
        gainFromOpen >= cfg.BuyPoint1MinChangePercent
        && gainFromOpen < cfg.BuyPoint2MinChangePercent;

    private static bool IsPullbackBuy1Eligible(
        MasterAlertOptions cfg,
        VipPullbackMaContext? pullbackMa,
        decimal liveClose,
        decimal gainFromOpen)
    {
        if (pullbackMa is null || !pullbackMa.Available)
            return false;

        if (cfg.PullbackRequireUptrendLong && !pullbackMa.UptrendLong)
            return false;

        if (gainFromOpen < cfg.PullbackMinGainFromOpenPercent)
            return false;

        return pullbackMa.IsNearMa(liveClose, cfg.PullbackNearMaPercent);
    }

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
