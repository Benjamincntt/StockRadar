using StockRadar.Domain.Entities;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.SellExit;

public sealed class AnchorWindowTests
{
    [Fact]
    public void Anchor_does_not_include_highs_before_entry()
    {
        var entry = new DateOnly(2026, 7, 10);
        var session = new DateOnly(2026, 7, 15);
        var history = new List<OhlcvBar>
        {
            Bar(new DateOnly(2026, 7, 1), high: 12m, close: 11m),
            Bar(new DateOnly(2026, 7, 2), high: 12m, close: 11m),
            Bar(entry, high: 9m, close: 8.5m),
            Bar(new DateOnly(2026, 7, 13), high: 9.2m, close: 9m),
            Bar(new DateOnly(2026, 7, 14), high: 9.1m, close: 8.8m),
        };

        var anchor = VipPositionHistoryCache.ComputeAnchorPrice(
            history, entry, session, lookbackSessions: 20, liveHigh: 8.6m);

        Assert.Equal(9.2m, anchor); // not 12
    }

    [Fact]
    public void Anchor_on_entry_day_is_live_high_when_no_prior_in_window()
    {
        var entry = new DateOnly(2026, 7, 10);
        var history = new List<OhlcvBar>
        {
            Bar(new DateOnly(2026, 7, 1), high: 12m, close: 11m),
        };

        var anchor = VipPositionHistoryCache.ComputeAnchorPrice(
            history, entry, entry, lookbackSessions: 20, liveHigh: 8.7m);

        Assert.Equal(8.7m, anchor);
    }

    private static OhlcvBar Bar(DateOnly d, decimal high, decimal close) =>
        new(d, close * 0.99m, high, close * 0.98m, close, 1_000_000);
}
