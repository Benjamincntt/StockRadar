using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Tests.SellExit;

public sealed class OverheadBoxTests
{
    [Fact]
    public void Finds_overhead_box_above_entry_price()
    {
        var history = BuildBoxThenBreak(boxLow: 10.3m, boxHigh: 11.7m, sessions: 22);
        var analyzer = new DarvasBreakoutAnalyzer();
        var cfg = DarvasBoxSettings.Default with
        {
            MaxBoxHeightPercent = 15m,
            BreakoutMaxBoxHeightPercent = 15m,
        };

        var box = analyzer.FindNearestOverheadBox(
            history,
            priceAbove: 9m,
            sessionDate: history[^1].Date.AddDays(1),
            minSessions: 20,
            maxSessions: 45,
            maxAgeSessions: 250,
            cfg);

        Assert.NotNull(box);
        Assert.True(box!.HasValidBox);
        Assert.True(box.BoxLow > 9m);
        Assert.InRange(box.BoxLow, 10.2m, 10.5m);
    }

    [Fact]
    public void Short_box_under_20_sessions_is_rejected()
    {
        var history = BuildBoxThenBreak(boxLow: 10.3m, boxHigh: 11.7m, sessions: 12);
        var analyzer = new DarvasBreakoutAnalyzer();
        var cfg = DarvasBoxSettings.Default with
        {
            MaxBoxHeightPercent = 15m,
            BreakoutMaxBoxHeightPercent = 15m,
        };

        var box = analyzer.FindNearestOverheadBox(
            history,
            priceAbove: 9m,
            sessionDate: history[^1].Date.AddDays(1),
            minSessions: 20,
            maxSessions: 45,
            maxAgeSessions: 250,
            cfg);

        Assert.Null(box);
    }

    /// <summary>Hộp Close dao động trong [boxLow, boxHigh], chạm đủ 2 cạnh, rồi vài phiên gãy xuống.</summary>
    internal static List<OhlcvBar> BuildBoxThenBreak(decimal boxLow, decimal boxHigh, int sessions)
    {
        var list = new List<OhlcvBar>();
        var day = new DateOnly(2026, 5, 4); // Monday
        for (var i = 0; i < sessions; i++)
        {
            // Xen kẽ chạm đáy / đỉnh để PassesDarvasBox đủ MinTop/BottomTouches
            var close = i % 2 == 0 ? boxLow : boxHigh;
            var open = (boxLow + boxHigh) / 2m;
            var high = Math.Min(boxHigh * 1.01m, close + 0.2m);
            var low = Math.Max(boxLow * 0.99m, close - 0.2m);
            list.Add(new OhlcvBar(day, open, high, low, close, 800_000));
            day = NextTradingDay(day);
        }

        // 3 phiên gãy
        var px = boxLow * 0.92m;
        for (var i = 0; i < 3; i++)
        {
            list.Add(new OhlcvBar(day, px, px * 1.01m, px * 0.99m, px, 900_000));
            day = NextTradingDay(day);
            px *= 0.99m;
        }

        return list;
    }

    private static DateOnly NextTradingDay(DateOnly d)
    {
        do
        {
            d = d.AddDays(1);
        } while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);

        return d;
    }
}
