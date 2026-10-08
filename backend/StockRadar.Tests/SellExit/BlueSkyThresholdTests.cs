using StockRadar.Domain.MasterAlerts;

namespace StockRadar.Tests.SellExit;

public sealed class BlueSkyThresholdTests
{
    [Fact]
    public void Anchor_100_price_96_is_SellHalf()
    {
        var pos = SellExitFixtures.Position(entry: 90m, peak: 100m);
        var signal = SellExitFixtures.Eval(pos, SellExitFixtures.Row(96m), anchor: 100m);
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
    public void Same_drawdown_same_result_regardless_of_phase()
    {
        // Q4: ngưỡng 4%/6% dùng thẳng giá trị config, không nhân hệ số pha
        var posUnfav = SellExitFixtures.Position(entry: 90m, peak: 100m);
        var posFav = SellExitFixtures.Position(entry: 90m, peak: 100m);

        var signalUnfav = SellExitFixtures.Eval(
            posUnfav, SellExitFixtures.Row(96m), anchor: 100m, phase: "Unfavorable");
        var signalFav = SellExitFixtures.Eval(
            posFav, SellExitFixtures.Row(96m), anchor: 100m, phase: "Favorable");

        // Both should be SellPoint1Half (4% drawdown = stop1)
        Assert.Equal(MasterAlertKinds.SellPoint1Half, signalUnfav);
        Assert.Equal(MasterAlertKinds.SellPoint1Half, signalFav);
    }
}
