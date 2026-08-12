using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Infrastructure.MarketData;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.VipAlerts;

public sealed class BullTrapBuyPathTests
{
    [Fact]
    public void Bull_trap_env_true_when_near_peak_and_not_Favorable()
    {
        var cfg = new MasterAlertOptions { BullTrapGateEnabled = true };
        Assert.True(TopOpportunityVipAlertEvaluator.IsBullTrapEnvironment(cfg, true, "Neutral"));
        Assert.False(TopOpportunityVipAlertEvaluator.IsBullTrapEnvironment(cfg, true, "Favorable"));
        Assert.False(TopOpportunityVipAlertEvaluator.IsBullTrapEnvironment(cfg, false, "Neutral"));
    }

    [Fact]
    public void Scale_in_gain_from_buy1_entry()
    {
        Assert.Equal(10m, TopOpportunityVipAlertEvaluator.GainFromEntryPercent(100m, 110m));
        Assert.Equal(0m, TopOpportunityVipAlertEvaluator.GainFromEntryPercent(0m, 110m));
    }

    [Fact]
    public void Dip_bounce_requires_uptrend_dip_and_green_session()
    {
        var cfg = new MasterAlertOptions();
        var ma = new VipPullbackMaContext(
            Available: true,
            Ma10: 100,
            Ma20: 99,
            Ma50: 95,
            UptrendLong: true,
            HasRecentDip: true);
        var green = new KbsPriceBoardClient.KbsBoardRow(
            "HCM", 100, 102, 99, 101.5m, 1_000_000, 1.5m,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.True(TopOpportunityVipAlertEvaluator.IsDipBounceBuy1Eligible(cfg, ma, green, 1.5m));

        var red = green with { Close = 99m, Open = 100m, ChangePercent = -1m };
        Assert.False(TopOpportunityVipAlertEvaluator.IsDipBounceBuy1Eligible(cfg, ma, red, -1m));

        var noDip = ma with { HasRecentDip = false };
        Assert.False(TopOpportunityVipAlertEvaluator.IsDipBounceBuy1Eligible(cfg, noDip, green, 1.5m));
    }

    [Fact]
    public void Count_recent_down_sessions()
    {
        var day = new DateOnly(2025, 1, 2);
        var bars = new List<OhlcvBar>
        {
            new(day, 100, 101, 99, 100, 1),
            new(day.AddDays(1), 100, 100, 98, 98, 1),
            new(day.AddDays(2), 98, 98, 96, 96, 1),
            new(day.AddDays(3), 96, 97, 95, 95.5m, 1),
        };
        Assert.Equal(3, VipPullbackMaContext.CountRecentDownSessions(bars, 3));
    }
}
