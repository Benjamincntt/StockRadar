using StockRadar.Domain.MasterAlerts;

namespace StockRadar.Tests.SellExit;

public sealed class BlueSkyStopTests
{
    [Fact]
    public void Losing_position_still_fires_SellHalf()
    {
        var pos = SellExitFixtures.Position(entry: 100m, peak: 100m, entryBarLow: 90m);
        // never profitable; anchor 100, price 96 → 4% drop
        var signal = SellExitFixtures.Eval(pos, SellExitFixtures.Row(96m), anchor: 100m);
        Assert.Equal(MasterAlertKinds.SellPoint1Half, signal);
    }

    [Fact]
    public void Breach_entry_bar_low_is_SellAll_even_before_6pct()
    {
        var pos = SellExitFixtures.Position(entry: 100m, peak: 105m, entryBarLow: 98m);
        // close 97.5 → below entry bar low; drop from anchor 105 is only ~7% wait...
        // drop from 105 to 97.5 = 7.14% which would also be SellAll
        // Use higher anchor window: close just under entryBarLow with small drop from a low anchor
        var signal = SellExitFixtures.Eval(
            pos,
            SellExitFixtures.Row(97.5m),
            anchor: 100m); // drop 2.5% < 6%, but below EntryBarLow 98
        Assert.Equal(MasterAlertKinds.SellAll, signal);
    }
}
