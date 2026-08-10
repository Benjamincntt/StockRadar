using StockRadar.Domain.MasterAlerts;

namespace StockRadar.Tests.SellExit;

public sealed class BlueSkyThresholdTests
{
    [Fact]
    public void Neutral_anchor_100_price_96_is_SellHalf()
    {
        var pos = SellExitFixtures.Position(entry: 90m, peak: 100m);
        var signal = SellExitFixtures.Eval(pos, SellExitFixtures.Row(96m), anchor: 100m, phase: "Neutral");
        Assert.Equal(MasterAlertKinds.SellPoint1Half, signal);
    }

    [Fact]
    public void After_half_sold_price_94_is_SellAll()
    {
        var pos = SellExitFixtures.Position(
            entry: 90m,
            peak: 100m,
            fired: [MasterAlertKinds.SellPoint1Half]);
        var signal = SellExitFixtures.Eval(pos, SellExitFixtures.Row(94m), anchor: 100m);
        Assert.Equal(MasterAlertKinds.SellAll, signal);
    }

    [Fact]
    public void Unfavorable_anchor_100_price_97_is_SellHalf()
    {
        // stop1 = 4 * 0.75 = 3% → 97 triggers
        var pos = SellExitFixtures.Position(entry: 90m, peak: 100m);
        var signal = SellExitFixtures.Eval(pos, SellExitFixtures.Row(97m), anchor: 100m, phase: "Unfavorable");
        Assert.Equal(MasterAlertKinds.SellPoint1Half, signal);
    }

    [Fact]
    public void Favorable_anchor_100_price_96_is_null()
    {
        // stop1 = 4 * 1.25 = 5% → 96 not enough
        var pos = SellExitFixtures.Position(entry: 90m, peak: 100m);
        var signal = SellExitFixtures.Eval(pos, SellExitFixtures.Row(96m), anchor: 100m, phase: "Favorable");
        Assert.Null(signal);
    }
}
