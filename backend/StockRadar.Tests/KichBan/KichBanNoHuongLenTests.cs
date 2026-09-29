using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using Xunit;

namespace StockRadar.Tests.KichBan;

/// <summary>
/// Kiểm thử kịch bản "Nổ hướng lên" (Breakout) — Bối cảnh, Hình thái, Cò kích hoạt, Kế hoạch.
/// Dữ liệu là các dãy nến giả lập được dựng để điều khiển từng điều kiện một cách tất định.
/// </summary>
public sealed class KichBanNoHuongLenTests
{
    private static KichBanNoHuongLen Tao() => new(new KichBanNoHuongLenOptions());

    // ===== Bộ sinh nến giả lập =====

    private static List<OhlcvBar> BarsFrom(
        IReadOnlyList<decimal> closes,
        IReadOnlyList<long> volumes,
        decimal spreadPct = 0.005m)
    {
        var d = new DateOnly(2025, 1, 1);
        var list = new List<OhlcvBar>(closes.Count);
        for (var i = 0; i < closes.Count; i++)
        {
            var c = closes[i];
            list.Add(new OhlcvBar(d.AddDays(i), c, c * (1 + spreadPct), c * (1 - spreadPct), c, volumes[i]));
        }
        return list;
    }

    /// <summary>Tăng đều — EMA20 &gt; EMA50, ADX rất cao (xu hướng rõ).</summary>
    private static List<OhlcvBar> TangDan(int n = 250, decimal start = 20m, decimal step = 0.05m, long vol = 1_000_000)
    {
        var closes = Enumerable.Range(0, n).Select(i => start + i * step).ToList();
        var vols = Enumerable.Repeat(vol, n).ToList();
        return BarsFrom(closes, vols);
    }

    /// <summary>Giảm đều — EMA20 &lt; EMA50.</summary>
    private static List<OhlcvBar> GiamDan(int n = 250, decimal start = 40m, decimal step = 0.05m, long vol = 1_000_000)
    {
        var closes = Enumerable.Range(0, n).Select(i => start - i * step).ToList();
        var vols = Enumerable.Repeat(vol, n).ToList();
        return BarsFrom(closes, vols);
    }

    /// <summary>Dao động ngang — ADX thấp (không có xu hướng).</summary>
    private static List<OhlcvBar> DaoDong(int n = 250, decimal center = 25m, decimal bienDo = 0.4m, long vol = 1_000_000)
    {
        var closes = Enumerable.Range(0, n).Select(i => center + (i % 2 == 0 ? bienDo : -bienDo)).ToList();
        var vols = Enumerable.Repeat(vol, n).ToList();
        return BarsFrom(closes, vols);
    }

    /// <summary>Giá phẳng rồi tăng tốc dần về cuối — dùng cho breakout + MACD histogram mở rộng.</summary>
    private static List<OhlcvBar> BreakoutBars(long vol = 1_000_000)
    {
        var closes = new List<decimal>();
        var vols = new List<long>();
        for (var i = 0; i < 230; i++) { closes.Add(25m); vols.Add(vol); }
        var gia = 25m;
        for (var k = 1; k <= 20; k++) { gia += 0.2m * k; closes.Add(gia); vols.Add(vol); }
        return BarsFrom(closes, vols);
    }

    // ===== Bối cảnh =====

    [Fact]
    public void BoiCanh_Dat_KhiEma20TrenEma50VaAdxCao()
    {
        var bars = TangDan();

        var ketQua = Tao().DanhGiaBoiCanh(bars);

        Assert.True(ketQua.Dat);
        Assert.Equal(2, ketQua.BangChungs.Count);
        Assert.True(ketQua.BangChungs[0].Dat); // EMA20 > EMA50
        Assert.True(ketQua.BangChungs[1].Dat); // ADX > 25
        Assert.All(ketQua.BangChungs, b => Assert.Equal(VaiTroChiBao.BoiCanh, b.VaiTro));
    }

    [Fact]
    public void BoiCanh_KhongDat_KhiEma20DuoiEma50()
    {
        var bars = GiamDan();

        var ketQua = Tao().DanhGiaBoiCanh(bars);

        Assert.False(ketQua.Dat);
        Assert.False(ketQua.BangChungs[0].Dat); // điều kiện EMA trượt
    }

    [Fact]
    public void BoiCanh_KhongDat_KhiAdxThap()
    {
        var bars = DaoDong();

        var ketQua = Tao().DanhGiaBoiCanh(bars);

        Assert.False(ketQua.BangChungs[1].Dat); // ADX < 25 khi giá đi ngang
    }

    // ===== Hình thái =====

    [Fact]
    public void HinhThai_Dat_KhiBollingerCoHopVaVolumeTeo()
    {
        // Giá phẳng (Bollinger width ≈ 0) + 5 phiên cuối volume teo còn 40% mức nền.
        var closes = Enumerable.Repeat(25m, 250).ToList();
        var vols = Enumerable.Repeat(1_000_000L, 250).ToList();
        for (var i = 245; i < 250; i++) vols[i] = 400_000;
        var bars = BarsFrom(closes, vols);

        var ketQua = Tao().DanhGiaHinhThai(bars);

        Assert.True(ketQua.Dat);
        Assert.True(ketQua.BangChungs[0].Dat); // Bollinger width < 0.04
        Assert.True(ketQua.BangChungs[1].Dat); // TB5/TB20 < 0.7
        Assert.All(ketQua.BangChungs, b => Assert.Equal(VaiTroChiBao.HinhThai, b.VaiTro));
    }

    [Fact]
    public void HinhThai_KhongDat_KhiBollingerRong()
    {
        // Giá dao động mạnh 20↔30 → Bollinger width lớn, nền không nén.
        var closes = Enumerable.Range(0, 250).Select(i => i % 2 == 0 ? 20m : 30m).ToList();
        var vols = Enumerable.Repeat(1_000_000L, 250).ToList();
        var bars = BarsFrom(closes, vols);

        var ketQua = Tao().DanhGiaHinhThai(bars);

        Assert.False(ketQua.Dat);
        Assert.False(ketQua.BangChungs[0].Dat); // Bollinger width > 0.04
    }

    // ===== Cò kích hoạt =====

    [Fact]
    public void Trigger_Dat_KhiVuotDinhVolumeNoVaMacdMoRong()
    {
        var bars = BreakoutBars();
        var dinh20 = bars.TakeLast(20).Max(b => b.High);
        var avg20 = bars.TakeLast(20).Average(b => (decimal)b.Volume);
        var giaHienTai = dinh20 + 1m;
        var volumeHienTai = (long)(avg20 * 2m);

        var ketQua = Tao().KiemTraCoKichHoat(bars, giaHienTai, volumeHienTai);

        Assert.True(ketQua.Dat);
        Assert.True(ketQua.BangChungs[0].Dat); // vượt đỉnh hộp
        Assert.True(ketQua.BangChungs[1].Dat); // volume > 1.5×
        Assert.True(ketQua.BangChungs[2].Dat); // MACD histogram mở rộng
    }

    [Fact]
    public void Trigger_KhongDat_KhiVolumeYeu()
    {
        var bars = BreakoutBars();
        var dinh20 = bars.TakeLast(20).Max(b => b.High);
        var avg20 = bars.TakeLast(20).Average(b => (decimal)b.Volume);
        var giaHienTai = dinh20 + 1m;
        var volumeHienTai = (long)(avg20 * 1.2m); // chỉ 1.2× < ngưỡng 1.5×

        var ketQua = Tao().KiemTraCoKichHoat(bars, giaHienTai, volumeHienTai);

        Assert.False(ketQua.Dat);
        Assert.False(ketQua.BangChungs[1].Dat); // volume không đạt
    }

    // ===== Kế hoạch giao dịch =====

    [Fact]
    public void KeHoach_TinhDungEntrySlTpTuDayDinhHopVaAtr()
    {
        var bars = BreakoutBars();
        var giaHienTai = 68m;

        var keHoach = Tao().TinhKeHoach(bars, giaHienTai);

        var dayHop = bars.TakeLast(20).Min(b => b.Low);
        var dinhHop = bars.TakeLast(20).Max(b => b.High);
        var chieuCao = dinhHop - dayHop;
        var atr = IndicatorMath.Atr(bars, 14);

        Assert.Equal(giaHienTai, keHoach.GiaVaoLenhMin);
        Assert.Equal(giaHienTai + 0.3m * atr, keHoach.GiaVaoLenhMax);
        Assert.Equal(dayHop - 0.5m * atr, keHoach.GiaDungLo);
        Assert.Equal(dinhHop + chieuCao * 1.0m, keHoach.GiaChotLoi1);
        Assert.Equal(dinhHop + chieuCao * 1.5m, keHoach.GiaChotLoi2);
        Assert.Contains("đáy hộp", keHoach.DieuKienHuy);
    }

    [Fact]
    public void ThieuLichSu_CacVaiTro_KhongDat()
    {
        var bars = TangDan(n: 30); // < 60 phiên tối thiểu

        var kichBan = Tao();
        Assert.False(kichBan.DanhGiaBoiCanh(bars).Dat);
        Assert.False(kichBan.DanhGiaHinhThai(bars).Dat);
        Assert.False(kichBan.KiemTraCoKichHoat(bars, 100m, 1_000_000).Dat);
    }
}
