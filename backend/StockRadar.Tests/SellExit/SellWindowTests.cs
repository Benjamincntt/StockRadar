using StockRadar.Domain.MasterAlerts;

namespace StockRadar.Tests.SellExit;

public sealed class SellWindowTests
{
    [Fact]
    public void Before_min_sessions_threshold_breach_is_RiskWarning_not_Sell()
    {
        var entry = new DateOnly(2026, 7, 6); // Mon
        var sameWeek = new DateOnly(2026, 7, 7); // Tue — 1 session
        var pos = SellExitFixtures.Position(entry: 100m, peak: 100m, entryDate: entry);
        var signal = SellExitFixtures.Eval(
            pos,
            SellExitFixtures.Row(96m),
            anchor: 100m,
            session: sameWeek);
        Assert.Equal(MasterAlertKinds.RiskWarningIntraday, signal);
    }

    [Fact]
    public void RiskWarning_does_not_repeat()
    {
        var entry = new DateOnly(2026, 7, 6);
        var sameWeek = new DateOnly(2026, 7, 7);
        var pos = SellExitFixtures.Position(
            entry: 100m,
            peak: 100m,
            entryDate: entry,
            fired: [MasterAlertKinds.RiskWarningIntraday]);
        var signal = SellExitFixtures.Eval(
            pos,
            SellExitFixtures.Row(96m),
            anchor: 100m,
            session: sameWeek);
        Assert.Null(signal);
    }
}
