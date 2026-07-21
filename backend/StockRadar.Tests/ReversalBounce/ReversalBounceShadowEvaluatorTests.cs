using StockRadar.Domain.Entities;
using StockRadar.Domain.Services.ReversalBounce;
using Xunit;

namespace StockRadar.Tests.ReversalBounce;

public sealed class ReversalBounceShadowEvaluatorTests
{
    private static readonly DateOnly Start = new(2026, 1, 5);
    private const decimal GapCancelMult = 1.5m;

    private static ReversalBounceTradeSettings Trade() =>
        new(
            TimeStopSessions: 10, MaxHoldSessions: 20, MinRewardToRisk: 1.5m,
            MinTradingSessionsToSell: 3, MaxSignalsPerDay: 5,
            SlippageBaseBps: 0m, SlippageGapImpactCoeff: 0m, SlippageFloorLockPenaltyBps: 0m,
            FeeBuyPercent: 0.15m, FeeSellPercent: 0.15m, TaxSellPercent: 0.10m);

    private static ReversalBounceTradePlan Plan(decimal invalidation, decimal target) =>
        new(20_000m, 21_000m, invalidation, target, 2m, 10, 0.5m, []);

    private static OhlcvBar Bar(int i, decimal open, decimal high, decimal low, decimal close) =>
        new(Start.AddDays(i), open, high, low, close, 1_000_000);

    private static ReversalBounceShadowInput Input(
        List<OhlcvBar> bars, ReversalBounceTradePlan plan,
        MarketRegime regime = MarketRegime.Stabilizing, string symbol = "TST", decimal atr = 0.03m) =>
        new(symbol, bars[0].Date, regime, "HOSE", bars, SignalIndex: 0, plan, atr);

    private static List<OhlcvBar> WinBars() =>
    [
        Bar(0, 20_000m, 20_000m, 20_000m, 20_000m),
        Bar(1, 20_000m, 20_100m, 19_900m, 20_000m),
        Bar(2, 20_000m, 20_200m, 19_900m, 20_000m),
        Bar(3, 20_000m, 20_400m, 19_900m, 20_000m),
        Bar(4, 21_000m, 22_100m, 20_900m, 22_000m), // s3 -> Target, net 9.6%
        Bar(5, 22_000m, 22_200m, 21_800m, 22_000m),
    ];

    private static List<OhlcvBar> LoseBars() =>
    [
        Bar(0, 20_000m, 20_000m, 20_000m, 20_000m),
        Bar(1, 20_000m, 20_100m, 17_900m, 18_000m),
        Bar(2, 18_000m, 18_100m, 17_800m, 18_000m),
        Bar(3, 18_000m, 18_100m, 17_800m, 18_000m),
        Bar(4, 18_000m, 18_100m, 17_800m, 18_000m), // s3 -> Stop, net ~ -10.4%
        Bar(5, 18_000m, 18_100m, 17_800m, 18_000m),
    ];

    private static ReversalBounceShadowSummary Run(params ReversalBounceShadowInput[] inputs) =>
        ReversalBounceShadowEvaluator.Evaluate(
            Start, Start.AddDays(30), inputs, GapCancelMult, Trade(), allowDefensiveEarlyExit: false);

    [Fact]
    public void TargetHit_CountsAsWin()
    {
        var r = Run(Input(WinBars(), Plan(18_000m, 22_000m)));

        Assert.Equal(1, r.TotalActionable);
        Assert.Equal(1, r.Measured);
        Assert.Equal(1, r.Win);
        Assert.Equal(100m, r.WinRatePercent);
        Assert.Equal(ReversalBounceShadowEvaluator.BucketWin, r.Trades[0].Bucket);
    }

    [Fact]
    public void StopHit_CountsAsLose()
    {
        var r = Run(Input(LoseBars(), Plan(19_000m, 24_000m)));

        Assert.Equal(1, r.Measured);
        Assert.Equal(1, r.Lose);
        Assert.Equal(0, r.Win);
        Assert.Equal(ReversalBounceShadowEvaluator.BucketLose, r.Trades[0].Bucket);
    }

    [Fact]
    public void GapUp_CountsAsGapCancelled_NotMeasured()
    {
        var bars = new List<OhlcvBar>
        {
            Bar(0, 20_000m, 20_000m, 20_000m, 20_000m),
            Bar(1, 21_000m, 21_200m, 20_900m, 21_100m), // gap +5% > 4.5%
            Bar(2, 21_100m, 21_300m, 21_000m, 21_200m),
        };

        var r = Run(Input(bars, Plan(19_000m, 24_000m)));

        Assert.Equal(1, r.GapCancelled);
        Assert.Equal(0, r.Measured);
        Assert.Equal("GapCancelled", r.Trades[0].Status);
    }

    [Fact]
    public void NoNextSession_CountsAsPending()
    {
        var bars = new List<OhlcvBar> { Bar(0, 20_000m, 20_000m, 20_000m, 20_000m) };

        var r = Run(Input(bars, Plan(19_000m, 24_000m)));

        Assert.Equal(1, r.Pending);
        Assert.Equal(0, r.Measured);
        Assert.Equal("Pending", r.Trades[0].Status);
    }

    [Fact]
    public void ByRegime_SplitsMeasuredTrades()
    {
        var r = Run(
            Input(WinBars(), Plan(18_000m, 22_000m), MarketRegime.Stabilizing, "AAA"),
            Input(WinBars(), Plan(18_000m, 22_000m), MarketRegime.Normal, "BBB"));

        Assert.Equal(2, r.Measured);
        Assert.Equal(2, r.ByRegime.Count);
        Assert.All(r.ByRegime, s => Assert.Equal(1, s.Measured));
    }
}
