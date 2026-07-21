using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Services.ReversalBounce;
using Xunit;

namespace StockRadar.Tests.ReversalBounce;

public sealed class CounterTrendDecisionEngineTests
{
    private static readonly ReversalBounceSettings Settings = new ReversalBounceOptions().ToSettings();
    private static readonly CounterTrendDecisionEngine Engine = new();

    private static ReversalBounceFeatures FlatFeatures(decimal close = 20_000m, decimal atr = 600m, decimal capitLow = 19_850m)
    {
        var bars = new List<OhlcvBar>();
        var d = new DateOnly(2026, 1, 5);
        for (var i = 0; i < 60; i++)
            bars.Add(new OhlcvBar(d.AddDays(i), close, close, close, close, 1_000_000));

        return new ReversalBounceFeatures(
            Symbol: "TST",
            Exchange: "HOSE",
            AsOfDate: bars[^1].Date,
            History: bars,
            IndexHistory: [],
            Ma20: close, Ma20Prev: close, Ma50: close,
            Atr: atr, Rsi: 40m,
            PeakHigh: close * 1.3m,
            CapitulationLow: capitLow,
            CapitulationClose: capitLow,
            CapitulationDate: bars[10].Date,
            CapitulationIndex: 10,
            DrawdownPercent: -25m,
            DrawdownInAtr: 3m);
    }

    private static ReversalBounceSetup SetupWith(
        ReversalBounceStage stage,
        MarketRegime regime,
        decimal total,
        ReversalBounceComponentScores scores) =>
        new(
            Symbol: "TST",
            TradingDate: new DateOnly(2026, 3, 3),
            Stage: stage,
            SetupId: Guid.NewGuid(),
            CapitulationDate: new DateOnly(2026, 2, 10),
            CapitulationLow: 19_850m,
            CapitulationClose: 19_900m,
            RecoveryAttemptCount: 1,
            ComponentScores: scores,
            TotalScore: total,
            MarketRegime: regime,
            StrategyVersion: Settings.StrategyVersion,
            AlgorithmParametersHash: "",
            SchemaVersion: 1,
            Reasons: []);

    private static readonly ReversalBounceComponentScores GoodScores =
        new(15m, 20m, 15m, 15m, 10m, 0m);

    [Fact]
    public void NullPlan_When_Not_Confirmed()
    {
        var setup = SetupWith(ReversalBounceStage.Stabilizing, MarketRegime.Normal, 90m, GoodScores);
        var signal = Engine.Decide(setup, FlatFeatures(), Settings);
        Assert.Null(signal.TradePlan);
    }

    [Fact]
    public void NullPlan_In_Panic_Regime()
    {
        var setup = SetupWith(ReversalBounceStage.Confirmed, MarketRegime.Panic, 95m, GoodScores);
        var signal = Engine.Decide(setup, FlatFeatures(), Settings);
        Assert.Null(signal.TradePlan);
    }

    [Fact]
    public void NullPlan_When_Score_Below_Threshold()
    {
        var lowScores = new ReversalBounceComponentScores(5m, 5m, 5m, 5m, 6m, 0m);
        var setup = SetupWith(ReversalBounceStage.Confirmed, MarketRegime.Normal, 26m, lowScores);
        var signal = Engine.Decide(setup, FlatFeatures(), Settings);
        Assert.Null(signal.TradePlan);
    }

    [Fact]
    public void NullPlan_When_RiskPenalty_Too_Negative()
    {
        var risky = new ReversalBounceComponentScores(15m, 20m, 15m, 15m, 10m, -8m);
        var setup = SetupWith(ReversalBounceStage.Confirmed, MarketRegime.Normal, 82m, risky);
        var signal = Engine.Decide(setup, FlatFeatures(), Settings);
        Assert.Null(signal.TradePlan);
    }

    [Fact]
    public void Plan_When_All_Hard_Gates_Pass()
    {
        var setup = SetupWith(ReversalBounceStage.Confirmed, MarketRegime.Normal, 82m, GoodScores);
        var signal = Engine.Decide(setup, FlatFeatures(), Settings);

        Assert.NotNull(signal.TradePlan);
        Assert.True(signal.TradePlan!.RewardToRisk >= Settings.Trade.MinRewardToRisk);
        Assert.True(signal.TradePlan.FirstTarget > signal.TradePlan.EntryReference);
        Assert.True(signal.TradePlan.InvalidationPrice < signal.TradePlan.EntryReference);
        Assert.Equal(Settings.RegimeGate.NormalPositionFactor, signal.TradePlan.PositionFactor);
    }
}
