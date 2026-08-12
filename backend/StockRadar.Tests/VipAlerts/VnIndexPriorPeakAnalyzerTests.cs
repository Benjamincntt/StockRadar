using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;

namespace StockRadar.Tests.VipAlerts;

public sealed class VnIndexPriorPeakAnalyzerTests
{
    [Fact]
    public void Near_prior_peak_with_non_Favorable_blocks_buy()
    {
        Assert.True(VnIndexPriorPeakAnalyzer.ShouldBlockBuy(true, nearPriorPeak: true, "Neutral"));
        Assert.True(VnIndexPriorPeakAnalyzer.ShouldBlockBuy(true, nearPriorPeak: true, "Unfavorable"));
        Assert.False(VnIndexPriorPeakAnalyzer.ShouldBlockBuy(true, nearPriorPeak: true, "Favorable"));
        Assert.False(VnIndexPriorPeakAnalyzer.ShouldBlockBuy(true, nearPriorPeak: false, "Neutral"));
        Assert.False(VnIndexPriorPeakAnalyzer.ShouldBlockBuy(false, nearPriorPeak: true, "Neutral"));
    }

    [Fact]
    public void Finds_recent_resistance_peak_when_index_below_it()
    {
        var day = new DateOnly(2025, 1, 2);
        var bars = new List<OhlcvBar>();

        // Base grind ~100
        for (var i = 0; i < 20; i++)
        {
            bars.Add(Bar(day, 100m + (i % 3) * 0.2m));
            day = day.AddDays(1);
        }

        // Rally to peak ~110
        for (var i = 0; i < 8; i++)
        {
            bars.Add(Bar(day, 100m + i * 1.25m));
            day = day.AddDays(1);
        }

        var peakDay = day.AddDays(-1);
        var peakPrice = bars[^1].High;

        // Pull back ~5%+
        for (var i = 0; i < 10; i++)
        {
            bars.Add(Bar(day, peakPrice * (1m - 0.006m * (i + 1))));
            day = day.AddDays(1);
        }

        // Rally back near peak (within 1.5%)
        var live = peakPrice * 0.990m;
        bars.Add(Bar(day, live));
        day = day.AddDays(1);

        var session = day;
        var peak = VnIndexPriorPeakAnalyzer.FindActiveResistance(
            bars, session, live, lookbackSessions: 60, pivotRadius: 2, minProminencePercent: 3m);

        Assert.NotNull(peak);
        Assert.True(peak!.Price > live);
        Assert.True(VnIndexPriorPeakAnalyzer.IsNearPriorPeak(peak, live, bandPercent: 1.5m));
        Assert.True(peak.ProminencePercent >= 3m);
        Assert.Equal(peakDay, peak.Date);
    }

    [Fact]
    public void No_near_peak_when_already_above_resistance()
    {
        var day = new DateOnly(2025, 1, 2);
        var bars = new List<OhlcvBar>();
        for (var i = 0; i < 30; i++)
        {
            bars.Add(Bar(day, 100m + i * 0.1m));
            day = day.AddDays(1);
        }

        // Small bump then continue higher (breakout)
        bars.Add(Bar(day, 104m));
        day = day.AddDays(1);
        bars.Add(Bar(day, 103.5m));
        day = day.AddDays(1);
        bars.Add(Bar(day, 105m));
        day = day.AddDays(1);

        var live = 106m;
        var peak = VnIndexPriorPeakAnalyzer.FindActiveResistance(
            bars, day.AddDays(1), live, lookbackSessions: 60, pivotRadius: 2, minProminencePercent: 3m);

        Assert.Null(peak);
        Assert.False(VnIndexPriorPeakAnalyzer.IsNearPriorPeak(peak, live, 1.5m));
    }

    [Fact]
    public void Prefers_nearest_peak_by_price_not_by_date()
    {
        // Live 1700 → overhead 1793 & 1850 → chọn 1793; 1642 đã dưới live → bỏ.
        var day = new DateOnly(2025, 1, 2);
        var bars = new List<OhlcvBar>();

        void Add(decimal o, decimal h, decimal l, decimal c)
        {
            bars.Add(new OhlcvBar(day, o, h, l, c, 1_000_000));
            day = day.AddDays(1);
        }

        // Đệm đủ cho pivot radius
        for (var i = 0; i < 10; i++)
            Add(1500, 1505, 1495, 1500);

        // Peak 1642 rồi rút sâu
        Add(1600, 1610, 1590, 1600);
        Add(1610, 1642, 1605, 1635); // swing high 1642
        Add(1620, 1625, 1580, 1585);
        Add(1580, 1585, 1540, 1550);
        Add(1550, 1560, 1520, 1530);

        for (var i = 0; i < 5; i++)
            Add(1550, 1560, 1540, 1550);

        // Peak 1793 rồi rút
        Add(1700, 1710, 1690, 1700);
        Add(1710, 1793, 1705, 1780); // swing high 1793
        Add(1770, 1775, 1680, 1690);
        Add(1690, 1700, 1620, 1630);

        for (var i = 0; i < 5; i++)
            Add(1650, 1660, 1640, 1650);

        // Peak 1850 (mới hơn, cao hơn) rồi về 1700
        Add(1750, 1760, 1740, 1750);
        Add(1760, 1850, 1755, 1840); // swing high 1850
        Add(1830, 1835, 1720, 1730);
        Add(1720, 1725, 1680, 1700);
        Add(1695, 1705, 1690, 1700);

        var live = 1700m;
        var peak = VnIndexPriorPeakAnalyzer.FindActiveResistance(
            bars, day, live, lookbackSessions: 750, pivotRadius: 2, minProminencePercent: 3m);

        Assert.NotNull(peak);
        Assert.Equal(1793m, peak!.Price);
    }

    private static OhlcvBar Bar(DateOnly date, decimal close) =>
        new(date, close * 0.99m, close * 1.005m, close * 0.985m, close, 1_000_000);
}
