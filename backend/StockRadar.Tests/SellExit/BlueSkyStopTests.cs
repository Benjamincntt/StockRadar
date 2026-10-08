using StockRadar.Domain.MasterAlerts;

namespace StockRadar.Tests.SellExit;

public sealed class BlueSkyStopTests
{
    [Fact]
    public void Losing_position_still_fires_SellHalf()
    {
        var pos = SellExitFixtures.Position(entry: 100m, peak: 100m);
        // never profitable; anchor 100, price 96 → 4% drop
        var signal = SellExitFixtures.Eval(pos, SellExitFixtures.Row(96m), anchor: 100m);
        Assert.Equal(MasterAlertKinds.SellPoint1Half, signal);
    }

    [Fact]
    public void Breach_entry_bar_low_no_longer_triggers_sell()
    {
        // After simplification: EntryBarLow logic removed.
        // close 97.5 → drop from anchor 100 is 2.5% < stop1 (4%) → no sell
        var pos = SellExitFixtures.Position(entry: 100m, peak: 105m, entryBarLow: 98m);
        var signal = SellExitFixtures.Eval(
            pos,
            SellExitFixtures.Row(97.5m),
            anchor: 100m);
        Assert.Null(signal);
    }
}
