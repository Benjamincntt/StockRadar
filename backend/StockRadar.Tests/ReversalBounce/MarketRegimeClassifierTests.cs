using StockRadar.Domain.Services.ReversalBounce;
using Xunit;

namespace StockRadar.Tests.ReversalBounce;

public sealed class MarketRegimeClassifierTests
{
    private static readonly MarketRegimeClassifier Classifier = new();
    private static readonly MarketRegimeThresholds Thresholds = MarketRegimeThresholds.Default;

    private static MarketBreadthSnapshot Snapshot(
        decimal pctAboveMa20,
        int floorCount,
        decimal drawdown,
        bool aboveMa20 = false,
        bool reclaimed = false,
        MarketRegime regime = MarketRegime.Normal,
        int improveStreak = 0) =>
        new(
            TradingDate: new DateOnly(2026, 3, 3),
            UniverseCount: 400,
            PctAboveMa20: pctAboveMa20,
            PctAboveMa50: pctAboveMa20,
            PctNewLow20: 0m,
            PctUp: 0m,
            PctDown: 0m,
            FloorCount: floorCount,
            CeilingCount: 0,
            MedianReturnPercent: 0m,
            MedianTurnover: 0m,
            VnIndexDrawdownPercent: drawdown,
            VnIndexDistanceToMa20Percent: 0m,
            VnIndexAboveMa20: aboveMa20,
            VnIndexReclaimedMa20: reclaimed,
            Regime: regime,
            ImproveStreak: improveStreak);

    [Fact]
    public void Enters_Panic_Immediately_When_Conditions_Met()
    {
        var current = Snapshot(pctAboveMa20: 12m, floorCount: 80, drawdown: -12m);
        var previous = Snapshot(pctAboveMa20: 40m, floorCount: 5, drawdown: -3m, regime: MarketRegime.Normal);

        var result = Classifier.Classify(current, previous, Thresholds);

        Assert.Equal(MarketRegime.Panic, result.Regime);
    }

    [Fact]
    public void Stays_Panic_Until_Enough_Improve_Streak()
    {
        // Phiên trước Panic, mới cải thiện 1 phiên (streak sẽ = 1 < 2) → vẫn Panic.
        var previous = Snapshot(pctAboveMa20: 15m, floorCount: 60, drawdown: -10m, regime: MarketRegime.Panic, improveStreak: 0);
        var current = Snapshot(pctAboveMa20: 25m, floorCount: 40, drawdown: -6m);

        var result = Classifier.Classify(current, previous, Thresholds);

        Assert.Equal(MarketRegime.Panic, result.Regime);
        Assert.Equal(1, result.ImproveStreak);
    }

    [Fact]
    public void Exits_Panic_To_Stabilizing_After_Streak_Reached()
    {
        // Phiên trước Panic đã có streak 1; phiên này cải thiện tiếp → streak 2 ≥ ngưỡng → thoát Panic.
        var previous = Snapshot(pctAboveMa20: 25m, floorCount: 40, drawdown: -6m, regime: MarketRegime.Panic, improveStreak: 1);
        var current = Snapshot(pctAboveMa20: 33m, floorCount: 25, drawdown: -4m);

        var result = Classifier.Classify(current, previous, Thresholds);

        Assert.Equal(MarketRegime.Stabilizing, result.Regime);
        Assert.Equal(2, result.ImproveStreak);
    }

    [Fact]
    public void Rebound_Drops_To_Stabilizing_When_Breadth_Worsens()
    {
        var previous = Snapshot(pctAboveMa20: 60m, floorCount: 3, drawdown: -1m, aboveMa20: true, regime: MarketRegime.ReboundConfirmed);
        var current = Snapshot(pctAboveMa20: 55m, floorCount: 4, drawdown: -2m, aboveMa20: true);

        var result = Classifier.Classify(current, previous, Thresholds);

        Assert.Equal(MarketRegime.Stabilizing, result.Regime);
    }
}
