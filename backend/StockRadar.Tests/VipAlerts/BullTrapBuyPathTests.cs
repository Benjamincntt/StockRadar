using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Infrastructure.MarketData;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.VipAlerts;

public sealed class BullTrapBuyPathTests
{
    [Fact]
    public void Bull_trap_env_true_when_near_peak_and_not_Favorable()
    {
        // BlockOnNeutral=true (opt-in): Neutral cũng bị block.
        var cfgStrict = new MasterAlertOptions { BullTrapGateEnabled = true, BullTrapBlockOnNeutral = true };
        Assert.True(TopOpportunityVipAlertEvaluator.IsBullTrapEnvironment(cfgStrict, true, "Neutral"));
        Assert.True(TopOpportunityVipAlertEvaluator.IsBullTrapEnvironment(cfgStrict, true, "Unfavorable"));
        Assert.False(TopOpportunityVipAlertEvaluator.IsBullTrapEnvironment(cfgStrict, true, "Favorable"));
        Assert.False(TopOpportunityVipAlertEvaluator.IsBullTrapEnvironment(cfgStrict, false, "Neutral"));

        // BlockOnNeutral=false (mặc định): Neutral bypass, chỉ Unfavorable kích hoạt.
        var cfgDefault = new MasterAlertOptions { BullTrapGateEnabled = true };
        Assert.False(TopOpportunityVipAlertEvaluator.IsBullTrapEnvironment(cfgDefault, true, "Neutral"));
        Assert.True(TopOpportunityVipAlertEvaluator.IsBullTrapEnvironment(cfgDefault, true, "Unfavorable"));
        Assert.False(TopOpportunityVipAlertEvaluator.IsBullTrapEnvironment(cfgDefault, true, "Favorable"));
    }

    [Fact]
    public void Scale_in_gain_from_buy1_entry()
    {
        Assert.Equal(10m, TopOpportunityVipAlertEvaluator.GainFromEntryPercent(100m, 110m));
        Assert.Equal(0m, TopOpportunityVipAlertEvaluator.GainFromEntryPercent(0m, 110m));
    }

    [Fact]
    public void Dip_bounce_requires_uptrend_dip_and_green_session()
    {
        var cfg = new MasterAlertOptions();
        var ma = new VipPullbackMaContext(
            Available: true,
            Ma10: 100,
            Ma20: 99,
            Ma50: 95,
            UptrendLong: true,
            HasRecentDip: true);
        var green = new KbsPriceBoardClient.KbsBoardRow(
            "HCM", 100, 102, 99, 101.5m, 1_000_000, 1.5m,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.True(TopOpportunityVipAlertEvaluator.IsDipBounceBuy1Eligible(cfg, ma, green, 1.5m));

        var red = green with { Close = 99m, Open = 100m, ChangePercent = -1m };
        Assert.False(TopOpportunityVipAlertEvaluator.IsDipBounceBuy1Eligible(cfg, ma, red, -1m));

        var noDip = ma with { HasRecentDip = false };
        Assert.False(TopOpportunityVipAlertEvaluator.IsDipBounceBuy1Eligible(cfg, noDip, green, 1.5m));
    }

    [Fact]
    public void Bull_trap_env_dropping_breakout_sets_blocked_flag_for_log()
    {
        // Ca DPM 03/09/2026: nổ +4.04% so giá mở cửa nhưng VNINDEX sát đỉnh cũ + pha Neutral,
        // dip-bounce trượt vì MA20 slope âm (UptrendLong=false) → bỏ tín hiệu KHÔNG dấu vết.
        // BullTrapBlockOnNeutral=true: test behavior khi Neutral bị chặn cứng (opt-in).
        var cfg = new MasterAlertOptions { BullTrapBlockOnNeutral = true, BullTrapSoftBlockEnabled = false };
        var state = new MasterAlertSessionTracker().GetOrReset("DPM", new DateOnly(2026, 9, 3));
        var ma = new VipPullbackMaContext(
            Available: true,
            Ma10: 22.03m,
            Ma20: 21.96m,
            Ma50: 21.77m,
            UptrendLong: false,
            HasRecentDip: true);
        var row = new KbsPriceBoardClient.KbsBoardRow(
            "DPM", 22.25m, 23.20m, 22.15m, 23.15m, 8_943_700, 5.23m,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var signal = TopOpportunityVipAlertEvaluator.EvaluateMasterSignal(
            cfg,
            state,
            ActionableEntry(),
            row,
            scan: null,
            pacedVolumeRatio: 3.5m,
            avgDailyVolume: 2_510_735,
            marketPhase: "Neutral",
            pullbackMa: ma,
            mlProb: 60m,
            mlModelActive: true,
            featuresComplete: true,
            resolvedMinMlProb: 52m,
            foreignNet: 0,
            orderflowObserved: false,
            indexNearPriorPeak: true,
            trapContextActive: false,
            liveIndexAbovePin: false,
            pastAfternoonCheckpoint: true,
            pinWindowIntegrityHeld: true,
            foreignNetSinceAfternoon: null,
            out _,
            out _,
            out _,
            out var blockedByBullTrap,
            out _);

        Assert.Null(signal);
        Assert.True(blockedByBullTrap);
    }

    [Fact]
    public void Bull_trap_env_allowing_dip_bounce_does_not_set_blocked_flag()
    {
        var cfg = new MasterAlertOptions();
        var state = new MasterAlertSessionTracker().GetOrReset("DPM", new DateOnly(2026, 9, 3));
        var ma = new VipPullbackMaContext(
            Available: true,
            Ma10: 22.03m,
            Ma20: 21.96m,
            Ma50: 21.77m,
            UptrendLong: true,
            HasRecentDip: true);
        var row = new KbsPriceBoardClient.KbsBoardRow(
            "DPM", 22.25m, 23.20m, 22.15m, 23.15m, 8_943_700, 5.23m,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        TopOpportunityVipAlertEvaluator.EvaluateMasterSignal(
            cfg,
            state,
            ActionableEntry(),
            row,
            scan: null,
            pacedVolumeRatio: 3.5m,
            avgDailyVolume: 2_510_735,
            marketPhase: "Neutral",
            pullbackMa: ma,
            mlProb: 60m,
            mlModelActive: true,
            featuresComplete: true,
            resolvedMinMlProb: 52m,
            foreignNet: 0,
            orderflowObserved: false,
            indexNearPriorPeak: true,
            trapContextActive: false,
            liveIndexAbovePin: false,
            pastAfternoonCheckpoint: true,
            pinWindowIntegrityHeld: true,
            foreignNetSinceAfternoon: null,
            out _,
            out _,
            out _,
            out var blockedByBullTrap,
            out _);

        Assert.False(blockedByBullTrap);
    }

    private static EntryPointDto ActionableEntry() => new(
        Status: "Ready",
        Type: "Breakout",
        Confidence: 73,
        EntryPrice: 23.1m,
        StopLoss: 21.78m,
        TriggerPrice: 22.67m,
        TargetPrice: 26.06m,
        BaseLow: 20.646m,
        BaseHigh: 22.45m,
        GainFromBasePercent: 2.9m,
        RiskRewardRatio: 2.24m,
        IsActionable: true,
        Headline: "Nổ hộp",
        Action: "Mua vùng trigger",
        Checklist: []);

    [Fact]
    public void Neutral_phase_bypasses_bull_trap_env_by_default_and_fires_buy1()
    {
        // E: BullTrapBlockOnNeutral=false (default) → Neutral không kích hoạt bull-trap env
        // → breakout hợp lệ đi qua path thường, deferral checkpoint không cản (pastAfternoonCheckpoint=true).
        var cfg = new MasterAlertOptions { RequiredConfirmationTicks = 1 }; // BullTrapBlockOnNeutral=false mặc định
        var state = new MasterAlertSessionTracker().GetOrReset("DPM", new DateOnly(2026, 9, 3));
        var ma = new VipPullbackMaContext(
            Available: true, Ma10: 22.03m, Ma20: 21.96m, Ma50: 21.77m,
            UptrendLong: false, HasRecentDip: true);
        var row = new KbsPriceBoardClient.KbsBoardRow(
            "DPM", 22.25m, 23.20m, 22.15m, 23.15m, 8_943_700, 5.23m,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var signal = TopOpportunityVipAlertEvaluator.EvaluateMasterSignal(
            cfg, state, ActionableEntry(), row, scan: null,
            pacedVolumeRatio: 3.5m, avgDailyVolume: 2_510_735,
            marketPhase: "Neutral", pullbackMa: ma,
            mlProb: 60m, mlModelActive: true, featuresComplete: true, resolvedMinMlProb: 52m,
            foreignNet: 0, orderflowObserved: false,
            indexNearPriorPeak: true, trapContextActive: false, liveIndexAbovePin: false,
            pastAfternoonCheckpoint: true, pinWindowIntegrityHeld: true,
            foreignNetSinceAfternoon: null,
            out _, out _, out _, out var blockedByBullTrap, out _);

        Assert.False(blockedByBullTrap);
        Assert.Equal(MasterAlertKinds.BuyPoint1, signal);
    }

    [Fact]
    public void Unfavorable_near_peak_softblock_defers_breakout_until_afternoon_checkpoint()
    {
        // F: BullTrapSoftBlockEnabled=true (default) + Unfavorable near peak →
        // breakout không hardblock mà bị defer (pastAfternoonCheckpoint=false → deferredByCheckpoint=true).
        var cfg = new MasterAlertOptions { BullTrapBlockOnNeutral = false }; // Unfavorable still triggers env
        var state = new MasterAlertSessionTracker().GetOrReset("DPM", new DateOnly(2026, 9, 3));
        var ma = new VipPullbackMaContext(
            Available: true, Ma10: 22.03m, Ma20: 21.96m, Ma50: 21.77m,
            UptrendLong: false, HasRecentDip: false);
        var row = new KbsPriceBoardClient.KbsBoardRow(
            "DPM", 22.25m, 23.20m, 22.15m, 23.15m, 8_943_700, 5.23m,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var signal = TopOpportunityVipAlertEvaluator.EvaluateMasterSignal(
            cfg, state, ActionableEntry(), row, scan: null,
            pacedVolumeRatio: 3.5m, avgDailyVolume: 2_510_735,
            marketPhase: "Unfavorable", pullbackMa: ma,
            mlProb: 60m, mlModelActive: true, featuresComplete: true, resolvedMinMlProb: 52m,
            foreignNet: 0, orderflowObserved: false,
            indexNearPriorPeak: true, trapContextActive: false, liveIndexAbovePin: false,
            pastAfternoonCheckpoint: false, pinWindowIntegrityHeld: true,
            foreignNetSinceAfternoon: null,
            out _, out var blockedByMl, out _, out var blockedByBullTrap, out var deferredByCheckpoint);

        Assert.Null(signal);
        Assert.False(blockedByBullTrap);   // softblock: không hardblock
        Assert.False(blockedByMl);
        Assert.True(deferredByCheckpoint); // chờ checkpoint chiều
    }

    [Fact]
    public void Count_recent_down_sessions()
    {
        var day = new DateOnly(2025, 1, 2);
        var bars = new List<OhlcvBar>
        {
            new(day, 100, 101, 99, 100, 1),
            new(day.AddDays(1), 100, 100, 98, 98, 1),
            new(day.AddDays(2), 98, 98, 96, 96, 1),
            new(day.AddDays(3), 96, 97, 95, 95.5m, 1),
        };
        Assert.Equal(3, VipPullbackMaContext.CountRecentDownSessions(bars, 3));
    }
}
