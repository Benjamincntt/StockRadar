using StockRadar.Domain.Entities;
using StockRadar.Domain.Services.ReversalBounce;
using Xunit;

namespace StockRadar.Tests.ReversalBounce;

public sealed class ReversalBounceFillSimulatorTests
{
    private const string Exchange = "HOSE";
    private const decimal AtrPercent = 0.03m; // ATR% = Atr/Close (phân số), 3%
    private const decimal GapCancelMult = 1.5m; // ngưỡng gap = 1.5 × 3% = 4.5%

    private static readonly DateOnly Start = new(2026, 1, 5);

    // Slippage = 0 để test giá/return tất định; phí thực tế 0.15% mua + 0.15% bán + 0.10% thuế.
    private static ReversalBounceTradeSettings Trade(int minSell = 3, int timeStop = 10, int maxHold = 20) =>
        new(
            TimeStopSessions: timeStop,
            MaxHoldSessions: maxHold,
            MinRewardToRisk: 1.5m,
            MinTradingSessionsToSell: minSell,
            MaxSignalsPerDay: 5,
            SlippageBaseBps: 0m,
            SlippageGapImpactCoeff: 0m,
            SlippageFloorLockPenaltyBps: 0m,
            FeeBuyPercent: 0.15m,
            FeeSellPercent: 0.15m,
            TaxSellPercent: 0.10m);

    private static ReversalBounceTradePlan Plan(decimal invalidation, decimal target) =>
        new(
            EntryReference: 20_000m,
            MaxEntryPrice: 21_000m,
            InvalidationPrice: invalidation,
            FirstTarget: target,
            RewardToRisk: 2m,
            TimeStopSessions: 10,
            PositionFactor: 0.5m,
            RiskWarnings: []);

    private static OhlcvBar Bar(int i, decimal open, decimal high, decimal low, decimal close) =>
        new(Start.AddDays(i), open, high, low, close, 1_000_000);

    // signalIndex = 0; entry ở bar[1].
    private static ReversalBounceFillResult Run(
        List<OhlcvBar> bars, ReversalBounceTradePlan plan, bool defensive = false, ReversalBounceTradeSettings? trade = null) =>
        ReversalBounceFillSimulator.Simulate(
            bars, signalIndex: 0, Exchange, plan, AtrPercent, GapCancelMult, trade ?? Trade(), defensive);

    [Fact]
    public void NoSellBeforeT3_EvenIfBelowInvalidation()
    {
        var bars = new List<OhlcvBar>
        {
            Bar(0, 20_000m, 20_000m, 20_000m, 20_000m), // T
            Bar(1, 20_000m, 20_000m, 17_900m, 18_000m), // s0 - dưới invalidation nhưng chưa được bán
            Bar(2, 18_000m, 18_100m, 17_800m, 18_000m), // s1
            Bar(3, 18_000m, 18_100m, 17_800m, 18_000m), // s2
            Bar(4, 18_000m, 18_100m, 17_800m, 18_000m), // s3 -> Stop
            Bar(5, 18_000m, 18_100m, 17_800m, 18_000m),
        };

        var result = Run(bars, Plan(invalidation: 19_000m, target: 24_000m));

        Assert.True(result.Entered);
        Assert.Equal(ReversalBounceExitReasons.Stop, result.ExitReason);
        Assert.Equal(3, result.SessionsToExit);
    }

    [Fact]
    public void DefensiveEarlyExit_TriggersOnSession1()
    {
        var bars = new List<OhlcvBar>
        {
            Bar(0, 20_000m, 20_000m, 20_000m, 20_000m), // T
            Bar(1, 20_000m, 20_100m, 19_800m, 20_000m), // s0 - trên invalidation
            Bar(2, 20_000m, 20_100m, 17_900m, 18_000m), // s1 - thủng invalidation -> defensive exit
            Bar(3, 18_000m, 18_100m, 17_800m, 18_000m),
            Bar(4, 18_000m, 18_100m, 17_800m, 18_000m),
        };

        var result = Run(bars, Plan(invalidation: 19_000m, target: 24_000m), defensive: true);

        Assert.Equal(ReversalBounceExitReasons.DefensiveExit, result.ExitReason);
        Assert.Equal(1, result.SessionsToExit);
    }

    [Fact]
    public void GapUpBeyondThreshold_CancelsEntry()
    {
        var bars = new List<OhlcvBar>
        {
            Bar(0, 20_000m, 20_000m, 20_000m, 20_000m), // T close 20_000
            Bar(1, 21_000m, 21_200m, 20_900m, 21_100m), // gap +5% > 4.5% -> cancel
            Bar(2, 21_100m, 21_300m, 21_000m, 21_200m),
        };

        var result = Run(bars, Plan(invalidation: 19_000m, target: 24_000m));

        Assert.False(result.Entered);
        Assert.Equal(ReversalBounceExitReasons.GapCancelled, result.ExitReason);
        Assert.Null(result.ExitPrice);
    }

    [Fact]
    public void FloorLockedExit_DefersToNextSession()
    {
        // s3 (bar[4]) đạt điều kiện Stop nhưng bị chất sàn (Close≈sàn & Close==Low); bar[5] không sàn -> hoãn bán sang bar[5].
        var bars = new List<OhlcvBar>
        {
            Bar(0, 20_000m, 20_000m, 20_000m, 20_000m), // T
            Bar(1, 20_000m, 20_100m, 19_800m, 20_000m), // s0
            Bar(2, 20_000m, 20_100m, 19_800m, 20_000m), // s1
            Bar(3, 20_000m, 20_100m, 19_800m, 20_000m), // s2 (prevClose->floor 18_600)
            Bar(4, 18_600m, 18_600m, 18_600m, 18_600m), // s3: chất sàn + Close<=invalidation -> Stop nhưng hoãn
            Bar(5, 20_000m, 20_100m, 19_500m, 20_000m), // s4: không sàn -> bán ở đây
            Bar(6, 20_000m, 20_100m, 19_800m, 20_000m),
        };

        var result = Run(bars, Plan(invalidation: 19_000m, target: 30_000m));

        Assert.Equal(ReversalBounceExitReasons.Stop, result.ExitReason);
        Assert.Equal(4, result.SessionsToExit);
        Assert.Equal(1, result.FloorDeferrals);
    }

    [Fact]
    public void TwoConsecutiveFloorLocks_ForcesExitAtOpen()
    {
        // bar[4] và bar[5] đều chất sàn -> ép bán Open bar[6].
        var bars = new List<OhlcvBar>
        {
            Bar(0, 20_000m, 20_000m, 20_000m, 20_000m), // T
            Bar(1, 20_000m, 20_100m, 19_800m, 20_000m), // s0
            Bar(2, 20_000m, 20_100m, 19_800m, 20_000m), // s1
            Bar(3, 20_000m, 20_100m, 19_800m, 20_000m), // s2 (floor 18_600)
            Bar(4, 18_600m, 18_600m, 18_600m, 18_600m), // s3: sàn (floor tiếp 17_300)
            Bar(5, 17_300m, 17_300m, 17_300m, 17_300m), // s4: sàn liên tiếp -> force
            Bar(6, 17_500m, 17_800m, 17_400m, 17_600m), // s5: ép bán ở Open=17_500
            Bar(7, 17_600m, 17_800m, 17_400m, 17_600m),
        };

        var result = Run(bars, Plan(invalidation: 19_000m, target: 30_000m));

        Assert.Equal(ReversalBounceExitReasons.FloorLockForced, result.ExitReason);
        Assert.Equal(5, result.SessionsToExit);
        Assert.Equal(1, result.FloorDeferrals);
    }

    [Fact]
    public void CleanTargetHit_IsWinNetOfFees()
    {
        var bars = new List<OhlcvBar>
        {
            Bar(0, 20_000m, 20_000m, 20_000m, 20_000m), // T
            Bar(1, 20_000m, 20_100m, 19_900m, 20_000m), // s0 entry@20_000
            Bar(2, 20_000m, 20_200m, 19_900m, 20_000m), // s1
            Bar(3, 20_000m, 20_400m, 19_900m, 20_000m), // s2
            Bar(4, 21_000m, 22_100m, 20_900m, 22_000m), // s3: Close>=target -> Target
            Bar(5, 22_000m, 22_200m, 21_800m, 22_000m),
        };

        var result = Run(bars, Plan(invalidation: 18_000m, target: 22_000m));

        Assert.True(result.Entered);
        Assert.Equal(ReversalBounceExitReasons.Target, result.ExitReason);
        Assert.Equal(3, result.SessionsToExit);
        // gross = (22_000-20_000)/20_000 = 10%; phí 0.4% -> net 9.6% -> Win (>=1%).
        Assert.Equal(10m, result.ReturnPercentGross);
        Assert.Equal(9.6m, result.ReturnPercentNet);
        Assert.True(result.ReturnPercentNet >= 1m);
    }
}
