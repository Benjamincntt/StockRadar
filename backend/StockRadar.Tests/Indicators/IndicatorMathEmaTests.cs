using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;
using Xunit;

namespace StockRadar.Tests.Indicators;

/// <summary>
/// G-BD-4 — chặn trôi giữa hai vòng hồi quy EMA: bản <c>Ema(IReadOnlyList&lt;decimal&gt;)</c>
/// (MACD dùng) và bản <c>EmaAt</c> trên nến (BaseQualityEvaluator dùng). Hai khối code riêng
/// nhưng bắt buộc cùng một công thức; test này là thứ thay cho việc gộp về một vòng.
/// </summary>
public sealed class IndicatorMathEmaTests
{
    private static List<OhlcvBar> Bars(params decimal[] closes)
    {
        var d = new DateOnly(2026, 1, 5);
        return closes
            .Select((c, i) => new OhlcvBar(d.AddDays(i), c, c * 1.01m, c * 0.99m, c, 500_000))
            .ToList();
    }

    private static List<decimal> Closes(IEnumerable<OhlcvBar> bars) =>
        bars.Select(b => b.Close).ToList();

    private static decimal[] Series(int count)
    {
        // Zig-zag có xu hướng tăng — đủ biến động để lệch seed lộ ra ngay.
        var values = new decimal[count];
        for (var i = 0; i < count; i++)
            values[i] = 100m + i * 0.7m + (i % 3 == 0 ? 2.4m : -1.1m);
        return values;
    }

    [Theory]
    [InlineData(5)]
    [InlineData(12)]
    [InlineData(20)]
    [InlineData(26)]
    public void EmaAt_TaiNenCuoi_TrungBanDecimal(int period)
    {
        var bars = Bars(Series(80));

        var viaBars = IndicatorMath.EmaAt(bars, bars.Count - 1, period);
        var viaDecimals = IndicatorMath.Ema(Closes(bars), period);

        Assert.Equal(viaDecimals, viaBars);
    }

    [Theory]
    [InlineData(10, 20)]
    [InlineData(10, 45)]
    [InlineData(20, 60)]
    [InlineData(26, 79)]
    public void EmaAt_GiuaChuoi_TrungPrefix(int period, int index)
    {
        var bars = Bars(Series(80));

        var atIndex = IndicatorMath.EmaAt(bars, index, period);
        var prefix = IndicatorMath.Ema(Closes(bars.Take(index + 1)), period);

        Assert.Equal(prefix, atIndex);
    }

    [Fact]
    public void Ema_TrenNen_LaWrapperCuaEmaAt_TaiNenCuoi()
    {
        var bars = Bars(Series(60));

        Assert.Equal(IndicatorMath.EmaAt(bars, bars.Count - 1, 20), IndicatorMath.Ema(bars, 20));
    }

    [Fact]
    public void ThieuDuLieu_CaHaiBan_TraTrungBinhToanChuoi()
    {
        var bars = Bars(Series(8));
        const int period = 20;

        var expected = IndicatorMath.AverageClose(bars, 0, bars.Count - 1);

        Assert.Equal(expected, IndicatorMath.EmaAt(bars, bars.Count - 1, period));
        Assert.Equal(expected, IndicatorMath.Ema(Closes(bars), period));
    }

    [Fact]
    public void ChuoiRong_TraKhong()
    {
        var bars = new List<OhlcvBar>();

        Assert.Equal(0m, IndicatorMath.EmaAt(bars, 0, 20));
        Assert.Equal(0m, IndicatorMath.Ema(bars, 20));
        Assert.Equal(0m, IndicatorMath.Ema(new List<decimal>(), 20));
    }

    [Fact]
    public void IndexVuotCuoiChuoi_KepVeNenCuoi()
    {
        var bars = Bars(Series(40));

        Assert.Equal(
            IndicatorMath.EmaAt(bars, bars.Count - 1, 20),
            IndicatorMath.EmaAt(bars, 999, 20));
    }
}
