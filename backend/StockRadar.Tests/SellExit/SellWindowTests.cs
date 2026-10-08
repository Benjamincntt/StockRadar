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

    /// <summary>Hồi quy VIB 07/10/2026: vị thế mới mua, giá = giá mua, chưa đủ T+2.5,
    /// không phân phối, rút từ mốc &lt; 4% → KHÔNG trả RiskWarningIntraday.</summary>
    [Fact]
    public void VIB_regression_fresh_position_at_entry_price_no_warning()
    {
        var entry = new DateOnly(2026, 7, 6); // Mon
        var buyMoment = new DateOnly(2026, 7, 7); // Tue — 1 session (chưa đủ T+2.5)
        var pos = SellExitFixtures.Position(entry: 100m, peak: 100m, entryDate: entry);
        // anchor = 100, close = 100 → drawdown = 0% < 4% (RiskWarningDrawdownFromPeakPercent)
        var signal = SellExitFixtures.Eval(
            pos,
            SellExitFixtures.Row(100m),
            anchor: 100m,
            session: buyMoment);
        Assert.Null(signal);
    }
}
