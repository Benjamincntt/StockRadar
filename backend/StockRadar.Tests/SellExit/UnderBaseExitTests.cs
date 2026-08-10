using StockRadar.Domain.MasterAlerts;

namespace StockRadar.Tests.SellExit;

public sealed class UnderBaseExitTests
{
    [Fact]
    public void Touch_near_overhead_low_is_SellHalf()
    {
        // buffer 0.5% → trigger at 10 * 0.995 = 9.95
        var pos = SellExitFixtures.Position(
            entry: 9m,
            peak: 9.8m,
            regime: MasterAlertExitRegimes.UnderBase,
            baseLow: 10m,
            baseHigh: 12m,
            entryBarLow: 8m);
        var signal = SellExitFixtures.Eval(pos, SellExitFixtures.Row(9.96m), anchor: 9.96m);
        Assert.Equal(MasterAlertKinds.SellPoint1Half, signal);
    }

    [Fact]
    public void After_half_close_back_under_base_low_is_SellAll()
    {
        var pos = SellExitFixtures.Position(
            entry: 9m,
            peak: 10.2m,
            regime: MasterAlertExitRegimes.UnderBase,
            baseLow: 10m,
            baseHigh: 12m,
            entryBarLow: 8m,
            fired: [MasterAlertKinds.SellPoint1Half]);
        var signal = SellExitFixtures.Eval(pos, SellExitFixtures.Row(9.8m), anchor: 10.2m);
        Assert.Equal(MasterAlertKinds.SellAll, signal);
    }

    [Fact]
    public void Price_above_base_high_does_not_force_sell_in_evaluator()
    {
        // Regime switch is publisher-side; evaluator with UnderBase still uses base rules
        var pos = SellExitFixtures.Position(
            entry: 9m,
            peak: 13m,
            regime: MasterAlertExitRegimes.UnderBase,
            baseLow: 10m,
            baseHigh: 12m,
            entryBarLow: 8m,
            fired: [MasterAlertKinds.SellPoint1Half]);
        // Close above base high but also above base low → no SellAll from under-base reverse
        var signal = SellExitFixtures.Eval(pos, SellExitFixtures.Row(12.5m), anchor: 13m);
        Assert.Null(signal);
    }
}
