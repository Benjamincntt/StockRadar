using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;
using Xunit;

namespace StockRadar.Tests.SectorWave;

/// <summary>
/// Phân kỳ dương RSI(14) — kiểu điểm vào thứ 3 bên cạnh breakout và shakeout.
/// </summary>
public sealed class RsiDivergenceTests
{
    private static readonly ISignalAnalyzer Signals = new SignalAnalyzer();

    private static OhlcvBar Bar(DateOnly date, decimal close, decimal low, decimal? open = null) =>
        new(date, open ?? close, Math.Max(close, open ?? close) * 1.005m, low, close, 600_000);

    /// <summary>Dựng chuỗi giá theo % thay đổi từng phiên, đáy nến = close × (1 − wickPercent).</summary>
    private static List<OhlcvBar> Build(IEnumerable<decimal> dailyChangePercents, decimal start = 50_000m)
    {
        var bars = new List<OhlcvBar>();
        var date = new DateOnly(2026, 3, 2);
        var close = start;
        var i = 0;
        foreach (var change in dailyChangePercents)
        {
            var open = close;
            close = Math.Round(close * (1m + change / 100m), 2);
            bars.Add(Bar(date.AddDays(i), close, Math.Min(open, close) * 0.995m, open));
            i++;
        }

        return bars;
    }

    [Fact]
    public void GiaDayThapHonNhungRsiDayCaoHon_ThiNhanPhanKyDuong()
    {
        // Giảm sâu → đáy 1 (RSI rất thấp) → hồi → giảm chậm về đáy 2 thấp hơn chút (RSI cao hơn) → nến xác nhận.
        var changes = new List<decimal>();
        changes.AddRange(Enumerable.Repeat(0.2m, 12));          // nền đi ngang
        changes.AddRange([-3m, -3.5m, -3m, -2.5m, -3m]);        // rơi mạnh → đáy 1 (RSI rất thấp)
        changes.AddRange(Enumerable.Repeat(1.3m, 4));           // hồi nhẹ
        changes.AddRange(Enumerable.Repeat(-0.9m, 9));          // trượt chậm xuống dưới đáy 1
        changes.Add(2.5m);                                      // nến xác nhận

        var history = Build(changes);

        Assert.True(Signals.IsBullishRsiDivergence(history));
    }

    [Fact]
    public void GiaVaRsiCungTaoDayThapHon_ThiKhongPhaiPhanKy()
    {
        var changes = new List<decimal>();
        changes.AddRange(Enumerable.Repeat(0.2m, 12));
        changes.AddRange([-2m, -2m, -2m]);                      // đáy 1 nhẹ
        changes.AddRange([1.5m, 1m]);
        changes.AddRange([-4m, -4.5m, -5m, -4m, -4.5m]);        // rơi mạnh hơn → RSI cũng thấp hơn
        changes.Add(1.5m);

        var history = Build(changes);

        Assert.False(Signals.IsBullishRsiDivergence(history));
    }

    [Fact]
    public void XuHuongTangDeu_ThiKhongCoPhanKy()
    {
        var history = Build(Enumerable.Repeat(0.8m, 35));

        Assert.False(Signals.IsBullishRsiDivergence(history));
    }

    [Fact]
    public void ThieuLichSu_ThiTraVeFalse()
    {
        var history = Build(Enumerable.Repeat(-1m, 10));

        Assert.False(Signals.IsBullishRsiDivergence(history));
    }
}
