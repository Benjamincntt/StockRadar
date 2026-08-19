using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;
using Xunit;

namespace StockRadar.Tests.Indicators;

/// <summary>
/// Chốt regression cho bản MACD cuộn tăng dần: phải trùng khớp <b>từng chữ số</b> với bản cũ
/// (cắt <c>Take(i).ToList()</c> rồi chạy lại EMA từ đầu). Bản cũ được giữ nguyên văn ở đây làm
/// tham chiếu — nếu ai đó đổi cách mồi EMA thì test này đỏ trước khi điểm MACD lệch.
/// </summary>
public sealed class IndicatorMathMacdTests
{
    /// <summary>Bản gốc trước tối ưu — chép nguyên văn, KHÔNG sửa.</summary>
    private static (decimal macd, decimal signal, decimal hist) MacdReference(
        IReadOnlyList<OhlcvBar> history)
    {
        var closes = history.Select(b => b.Close).ToList();
        if (closes.Count < 26) return (0, 0, 0);

        var macdSeries = new List<decimal>();
        for (var i = 26; i <= closes.Count; i++)
        {
            var slice = closes.Take(i).ToList();
            macdSeries.Add(IndicatorMath.Ema(slice, 12) - IndicatorMath.Ema(slice, 26));
        }

        var macd = macdSeries[^1];
        var signal = macdSeries.Count >= 9 ? IndicatorMath.Ema(macdSeries, 9) : macd;
        return (macd, signal, macd - signal);
    }

    private static List<OhlcvBar> Bars(IEnumerable<decimal> closes)
    {
        var d = new DateOnly(2026, 1, 5);
        return closes
            .Select((c, i) => new OhlcvBar(d.AddDays(i), c, c * 1.01m, c * 0.99m, c, 500_000))
            .ToList();
    }

    /// <summary>Xu hướng tăng có nhiễu.</summary>
    private static List<OhlcvBar> TrendUp(int count) =>
        Bars(Enumerable.Range(0, count).Select(i => 100m + i * 0.7m + (i % 3 == 0 ? 2.4m : -1.1m)));

    /// <summary>Xu hướng giảm có nhiễu.</summary>
    private static List<OhlcvBar> TrendDown(int count) =>
        Bars(Enumerable.Range(0, count).Select(i => 300m - i * 0.9m + (i % 4 == 0 ? 3.1m : -0.6m)));

    /// <summary>Đi ngang biên độ hẹp — MACD quanh 0, dễ lộ sai số.</summary>
    private static List<OhlcvBar> Sideway(int count) =>
        Bars(Enumerable.Range(0, count).Select(i => 50m + (i % 5 - 2) * 0.15m));

    /// <summary>Gãy trend giữa chuỗi.</summary>
    private static List<OhlcvBar> Whipsaw(int count) =>
        Bars(Enumerable.Range(0, count).Select(i =>
            i < count / 2 ? 80m + i * 1.3m : 80m + (count - i) * 1.1m));

    public static TheoryData<string, int> Cases()
    {
        var data = new TheoryData<string, int>();
        foreach (var shape in new[] { "up", "down", "flat", "whip" })
        foreach (var len in new[] { 26, 27, 33, 34, 35, 60, 120, 400 })
            data.Add(shape, len);
        return data;
    }

    private static List<OhlcvBar> Build(string shape, int len) => shape switch
    {
        "up" => TrendUp(len),
        "down" => TrendDown(len),
        "flat" => Sideway(len),
        _ => Whipsaw(len)
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void BanCuonTangDan_TrungKhopBanCu(string shape, int len)
    {
        var bars = Build(shape, len);

        var expected = MacdReference(bars);
        var actual = IndicatorMath.Macd(bars);

        Assert.Equal(expected.macd, actual.macd);
        Assert.Equal(expected.signal, actual.signal);
        Assert.Equal(expected.hist, actual.hist);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(25)]
    public void ThieuDuLieu_TraKhong_GiongBanCu(int len)
    {
        var bars = TrendUp(len);

        Assert.Equal(MacdReference(bars), IndicatorMath.Macd(bars));
        Assert.Equal((0m, 0m, 0m), IndicatorMath.Macd(bars));
    }

    /// <summary>
    /// Chuỗi MACD ngắn hơn 9 phần tử (n từ 26..33) → signal phải rơi về chính macd,
    /// đúng như nhánh <c>Count &gt;= 9 ? Ema(...) : macd</c> của bản cũ.
    /// </summary>
    [Theory]
    [InlineData(26)]
    [InlineData(30)]
    [InlineData(33)]
    public void ChuoiMacdNganHon9_SignalBangMacd(int len)
    {
        var bars = TrendUp(len);

        var (macd, signal, hist) = IndicatorMath.Macd(bars);

        Assert.Equal(macd, signal);
        Assert.Equal(0m, hist);
        Assert.Equal(MacdReference(bars), (macd, signal, hist));
    }

    [Fact]
    public void ChuoiDai_KhongCon_ONBinhPhuong()
    {
        // 5000 phiên: bản cũ cấp phát ~5000 list (tổng ~12.5 triệu phần tử).
        // Bản mới duyệt một lượt — mốc thời gian ở đây chỉ để chặn hồi quy thuật toán.
        var bars = TrendUp(5000);

        var started = System.Diagnostics.Stopwatch.StartNew();
        var actual = IndicatorMath.Macd(bars);
        started.Stop();

        Assert.NotEqual(0m, actual.macd);
        Assert.True(
            started.ElapsedMilliseconds < 200,
            $"MACD 5000 phiên mất {started.ElapsedMilliseconds}ms — nghi ngờ quay lại O(n²).");
    }
}
