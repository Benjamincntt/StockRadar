using StockRadar.Domain.Entities;

namespace StockRadar.Tests.ReversalBounce;

internal static class OhlcvFixtures
{
    private static readonly DateOnly Start = new(2025, 1, 6);

    private static OhlcvBar Bar(int dayOffset, decimal open, decimal high, decimal low, decimal close, long volume) =>
        new(Start.AddDays(dayOffset), open, high, low, close, volume);

    /// <summary>Xu hướng tăng đều — không có drawdown → Stage None.</summary>
    public static List<OhlcvBar> SteadyUptrend(int n, decimal start = 20_000m)
    {
        var bars = new List<OhlcvBar>(n);
        var price = start;
        for (var i = 0; i < n; i++)
        {
            var open = price;
            var close = price * 1.005m;
            var high = close * 1.008m;
            var low = open * 0.996m;
            bars.Add(Bar(i, open, high, low, close, 1_000_000));
            price = close;
        }
        return bars;
    }

    /// <summary>
    /// Uptrend → đỉnh → bán tháo mạnh (~28%, nến biên rộng) → đi ngang co hẹp → 1 phiên xác nhận cầu.
    /// Đủ để analyzer bắt được capitulation và (thường) tiến tới Stabilizing/Confirmed.
    /// </summary>
    public static List<OhlcvBar> CapitulationStabilizationConfirmed()
    {
        var bars = new List<OhlcvBar>();
        var day = 0;

        // 1) Uptrend 40 phiên tới đỉnh ~ 30_000
        var price = 22_000m;
        for (var i = 0; i < 40; i++)
        {
            var open = price;
            var close = price * 1.005m;
            bars.Add(Bar(day++, open, close * 1.006m, open * 0.997m, close, 1_000_000));
            price = close;
        }
        var peak = price; // ~ 26_8xx

        // 2) Bán tháo 10 phiên, nến giảm biên rộng, volume lớn
        for (var i = 0; i < 10; i++)
        {
            var open = price;
            var close = price * 0.965m;
            var high = open * 1.002m;
            var low = close * 0.985m;
            bars.Add(Bar(day++, open, high, low, close, 3_000_000));
            price = close;
        }
        var capitLow = price * 0.985m; // đáy vùng bán tháo

        // 3) Đi ngang co hẹp 16 phiên, không thủng đáy, biên nhỏ, rút chân
        for (var i = 0; i < 16; i++)
        {
            var open = price;
            var close = price * 1.001m;
            var high = close * 1.004m;
            var low = Math.Max(capitLow, open * 0.997m);
            bars.Add(Bar(day++, open, high, low, close, 700_000));
            price = close;
        }

        // 4) Phiên xác nhận: đóng cửa mạnh vượt đỉnh gần, volume bung, gap nhỏ
        var recentHigh = bars.TakeLast(3).Max(b => b.High);
        var cOpen = price * 1.001m;
        var cClose = recentHigh * 1.02m;
        var cHigh = cClose * 1.001m;
        var cLow = cOpen * 0.999m;
        bars.Add(Bar(day, cOpen, cHigh, cLow, cClose, 2_500_000));

        _ = peak;
        return bars;
    }

    /// <summary>History dài để kiểm tra ATR không bị O(n²) (chạy nhanh + không lỗi).</summary>
    public static List<OhlcvBar> LongHistoryForAtrLoopTest(int n = 5000)
    {
        var bars = new List<OhlcvBar>(n);
        var price = 20_000m;
        var rng = new Random(42);
        for (var i = 0; i < n; i++)
        {
            var drift = (decimal)(rng.NextDouble() - 0.5) * 400m;
            var open = price;
            var close = Math.Max(1_000m, price + drift);
            var high = Math.Max(open, close) * 1.01m;
            var low = Math.Min(open, close) * 0.99m;
            bars.Add(Bar(i, open, high, low, close, 1_000_000));
            price = close;
        }
        return bars;
    }
}
