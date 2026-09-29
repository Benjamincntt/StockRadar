using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using Xunit;

namespace StockRadar.Tests.KichBan;

/// <summary>
/// Kiểm thử kịch bản "Kiệt sức" (Exhaustion — SELL): tăng nóng, hết đà → bán 50%.
/// </summary>
public sealed class KichBanKietSucTests
{
    /// <summary>Options dùng chung — test suy ra ngưỡng từ đây thay vì hardcode.</summary>
    private static readonly KichBanKietSucOptions Nguong = new();

    private static KichBanKietSuc Tao() => new(Nguong);

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

    private static List<OhlcvBar> TangDan(int n = 250, decimal start = 20m, decimal step = 0.03m, long vol = 1_000_000)
    {
        var closes = Enumerable.Range(0, n).Select(i => start + i * step).ToList();
        return BarsFrom(closes, Enumerable.Repeat(vol, n).ToList());
    }

    private static List<OhlcvBar> GiamDan(int n = 250, decimal start = 40m, decimal step = 0.05m, long vol = 1_000_000)
    {
        var closes = Enumerable.Range(0, n).Select(i => start - i * step).ToList();
        return BarsFrom(closes, Enumerable.Repeat(vol, n).ToList());
    }

    private static List<OhlcvBar> Flat(int n = 250, decimal gia = 25m, long vol = 1_000_000) =>
        BarsFrom(Enumerable.Repeat(gia, n).ToList(), Enumerable.Repeat(vol, n).ToList());

    /// <summary>Tăng tốc dần (gia tốc) — giá chạy rất xa EMA20 → Bối cảnh ĐẠT.</summary>
    private static List<OhlcvBar> SurgeBars(long vol = 1_000_000)
    {
        var closes = new List<decimal>();
        var vols = new List<long>();
        for (var i = 0; i < 230; i++) { closes.Add(25m); vols.Add(vol); }
        var gia = 25m;
        for (var k = 1; k <= 20; k++) { gia += 0.2m * k; closes.Add(gia); vols.Add(vol); }
        return BarsFrom(closes, vols);
    }

    /// <summary>Tăng nhưng gia tốc GIẢM dần + phiên cuối volume đỉnh → Hình thái ĐẠT (climax).</summary>
    private static List<OhlcvBar> ClimaxBars(long vol = 1_000_000)
    {
        var closes = new List<decimal>();
        var vols = new List<long>();
        for (var i = 0; i < 230; i++) { closes.Add(25m); vols.Add(vol); }
        var gia = 25m;
        for (var k = 0; k < 20; k++) { gia += 2.0m - 0.09m * k; closes.Add(gia); vols.Add(vol); }
        vols[^1] = VolClimax(vol); // volume climax phiên cuối
        return BarsFrom(closes, vols);
    }

    /// <summary>
    /// Volume phiên climax sao cho tỷ lệ so với TB20 vượt <c>MinVolumeClimax</c>.
    /// Vì chính phiên climax nằm trong cửa sổ TB20: ratio = 20v / (19×vol + v) &gt; ngưỡng
    /// → v &gt; vol × 19 × ngưỡng / (20 − ngưỡng). Cộng thêm 10% biên an toàn.
    /// </summary>
    private static long VolClimax(long vol)
    {
        var mau = Math.Max(1m, 20m - Nguong.MinVolumeClimax);
        var toiThieu = vol * 19m * Nguong.MinVolumeClimax / mau;
        return (long)Math.Ceiling(toiThieu * 1.1m);
    }

    // ===== Metadata =====

    [Fact]
    public void Metadata_LaKichBanBan()
    {
        var kichBan = Tao();
        Assert.Equal(LoaiKichBan.KietSuc, kichBan.LoaiKichBan);
        Assert.True(kichBan.LaKichBanBan);
    }

    // ===== Bối cảnh =====

    [Fact]
    public void BoiCanh_Dat_KhiTangNongVaGianXa()
    {
        var ketQua = Tao().DanhGiaBoiCanh(SurgeBars());

        Assert.True(ketQua.Dat);
        Assert.True(ketQua.BangChungs[0].Dat); // gain > MinGainFromEntry
        Assert.True(ketQua.BangChungs[1].Dat); // giãn > MinExtensionAtr×ATR
    }

    [Fact]
    public void BoiCanh_KhongDat_KhiTangDeu()
    {
        var ketQua = Tao().DanhGiaBoiCanh(TangDan());

        Assert.False(ketQua.Dat);
        Assert.False(ketQua.BangChungs[0].Dat); // gain < MinGainFromEntry
    }

    [Fact]
    public void BoiCanh_Overload_DungGiaVaoLenhThucTe()
    {
        var bars = SurgeBars();
        var close = bars[^1].Close;
        // Giá vào lệnh thực tế sát giá hiện tại → lãi ~1% (< MinGainFromEntry) → không đạt dù proxy SMA20 vẫn lãi lớn.
        var ketQua = Tao().DanhGiaBoiCanh(bars, giaVaoLenhThucTe: close * 0.99m);

        Assert.False(ketQua.BangChungs[0].Dat);
    }

    // ===== Hình thái =====

    [Fact]
    public void HinhThai_Dat_KhiCoItNhat2Trong3DauHieu()
    {
        var ketQua = Tao().DanhGiaHinhThai(ClimaxBars());

        Assert.True(ketQua.Dat);
        Assert.True(ketQua.BangChungs[0].Dat); // RSI > MinRsi
        Assert.True(ketQua.BangChungs[1].Dat); // volume climax > MinVolumeClimax
        Assert.True(ketQua.BangChungs[2].Dat); // MACD histogram giảm
    }

    [Fact]
    public void HinhThai_KhongDat_KhiGiamDan()
    {
        // RSI thấp, volume đều → không đủ 2/3 dấu hiệu phân phối.
        var ketQua = Tao().DanhGiaHinhThai(GiamDan());

        Assert.False(ketQua.Dat);
        Assert.False(ketQua.BangChungs[0].Dat); // RSI < MinRsi
    }

    // ===== Cò kích hoạt =====

    [Fact]
    public void Trigger_Dat_KhiGiaQuayDauDuoiBollingerUpper()
    {
        var bars = ClimaxBars();
        var (upper, _, _, _, _) = IndicatorMath.Bollinger(bars, 20);
        var giaHienTai = upper - 0.5m; // dưới biên trên

        var ketQua = Tao().KiemTraCoKichHoat(bars, giaHienTai, 1_000_000);

        Assert.True(ketQua.Dat);
        Assert.True(ketQua.BangChungs[1].Dat); // quay đầu dưới upper
    }

    [Fact]
    public void Trigger_KhongDat_KhiGiaVanTrenUpperVaKhongPhanKy()
    {
        var bars = Flat(); // Bollinger upper = 25 (std = 0), không có đỉnh mới
        var ketQua = Tao().KiemTraCoKichHoat(bars, giaHienTai: 26m, 1_000_000);

        Assert.False(ketQua.Dat);
        Assert.False(ketQua.BangChungs[0].Dat); // không phân kỳ âm
        Assert.False(ketQua.BangChungs[1].Dat); // giá trên upper
    }

    // ===== Kế hoạch (SELL → toàn 0) =====

    [Fact]
    public void KeHoach_Sell_KhongCoEntrySlTp()
    {
        var keHoach = Tao().TinhKeHoach(ClimaxBars(), 47m);

        Assert.Equal(0m, keHoach.GiaVaoLenhMin);
        Assert.Equal(0m, keHoach.GiaVaoLenhMax);
        Assert.Equal(0m, keHoach.GiaDungLo);
        Assert.Equal(0m, keHoach.GiaChotLoi1);
        Assert.Equal(0m, keHoach.GiaChotLoi2);
        Assert.Contains("RSI", keHoach.DieuKienHuy);
    }

    [Fact]
    public void ThieuLichSu_CacVaiTro_KhongDat()
    {
        var bars = SurgeBars();
        var ngan = bars.Take(30).ToList();

        var kichBan = Tao();
        Assert.False(kichBan.DanhGiaBoiCanh(ngan).Dat);
        Assert.False(kichBan.DanhGiaHinhThai(ngan).Dat);
        Assert.False(kichBan.KiemTraCoKichHoat(ngan, 100m, 1_000_000).Dat);
    }
}
