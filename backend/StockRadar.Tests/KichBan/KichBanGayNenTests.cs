using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using Xunit;

namespace StockRadar.Tests.KichBan;

/// <summary>
/// Kiểm thử kịch bản "Gãy nền" (Breakdown — EXIT/SELL): phá vỡ hỗ trợ → bán 100%.
/// </summary>
public sealed class KichBanGayNenTests
{
    private static KichBanGayNen Tao() => new(new KichBanGayNenOptions());

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

    private static List<OhlcvBar> TangDan(int n = 250, decimal start = 20m, decimal step = 0.05m, long vol = 1_000_000)
    {
        var closes = Enumerable.Range(0, n).Select(i => start + i * step).ToList();
        return BarsFrom(closes, Enumerable.Repeat(vol, n).ToList());
    }

    private static List<OhlcvBar> GiamDan(int n = 250, decimal start = 40m, decimal step = 0.05m, long vol = 1_000_000)
    {
        var closes = Enumerable.Range(0, n).Select(i => start - i * step).ToList();
        return BarsFrom(closes, Enumerable.Repeat(vol, n).ToList());
    }

    /// <summary>Nền phẳng 25 rồi 10 phiên gãy giảm 0.3/phiên, phiên cuối volume bán mạnh (2×).</summary>
    private static List<OhlcvBar> BreakdownBars(long volCuoi = 2_000_000, long vol = 1_000_000)
    {
        var closes = new List<decimal>();
        var vols = new List<long>();
        for (var i = 0; i < 240; i++) { closes.Add(25m); vols.Add(vol); }
        var gia = 25m;
        for (var k = 0; k < 10; k++) { gia -= 0.3m; closes.Add(gia); vols.Add(vol); }
        vols[^1] = volCuoi;
        return BarsFrom(closes, vols);
    }

    // ===== Metadata =====

    [Fact]
    public void Metadata_LaKichBanBan()
    {
        var kichBan = Tao();
        Assert.Equal(LoaiKichBan.GayNen, kichBan.LoaiKichBan);
        Assert.True(kichBan.LaKichBanBan);
    }

    // ===== Bối cảnh (luôn đạt — kiểm tra vị thế ở caller) =====

    [Fact]
    public void BoiCanh_LuonDat()
    {
        var ketQua = Tao().DanhGiaBoiCanh(GiamDan());

        Assert.True(ketQua.Dat);
        Assert.Single(ketQua.BangChungs);
        Assert.Contains("vị thế", ketQua.BangChungs[0].MoTa);
    }

    // ===== Hình thái =====

    [Fact]
    public void HinhThai_Dat_KhiMatHoTro()
    {
        // Downtrend: close < EMA20 → mất hỗ trợ.
        var ketQua = Tao().DanhGiaHinhThai(GiamDan());

        Assert.True(ketQua.Dat);
        Assert.True(ketQua.BangChungs[0].Dat); // có đáy nền
        Assert.True(ketQua.BangChungs[1].Dat); // mất ≥ 1 hỗ trợ
    }

    [Fact]
    public void HinhThai_KhongDat_KhiTrenMoiHoTro()
    {
        // Uptrend: close trên đáy nền, EMA20 và VWAP.
        var ketQua = Tao().DanhGiaHinhThai(TangDan());

        Assert.False(ketQua.Dat);
        Assert.False(ketQua.BangChungs[1].Dat);
    }

    // ===== Cò kích hoạt =====

    [Fact]
    public void Trigger_Dat_KhiVolumeManhMacdAmVaGayDayNen()
    {
        var bars = BreakdownBars();
        var giaHienTai = bars[^1].Close; // 22.0 < đáy nền

        var ketQua = Tao().KiemTraCoKichHoat(bars, giaHienTai, volumeHienTai: 2_000_000);

        Assert.True(ketQua.Dat);
        Assert.True(ketQua.BangChungs[0].Dat); // volume bán mạnh
        Assert.True(ketQua.BangChungs[1].Dat); // MACD âm liên tiếp
        Assert.True(ketQua.BangChungs[2].Dat); // đóng cửa dưới đáy nền
    }

    [Fact]
    public void Trigger_KhongDat_KhiVolumeYeu()
    {
        var bars = BreakdownBars(volCuoi: 1_000_000);
        var giaHienTai = bars[^1].Close;

        var ketQua = Tao().KiemTraCoKichHoat(bars, giaHienTai, volumeHienTai: 1_050_000);

        Assert.False(ketQua.Dat);
        Assert.False(ketQua.BangChungs[0].Dat); // volume không đạt 1.5×
    }

    // ===== Kế hoạch (EXIT → toàn 0, không có điều kiện hủy) =====

    [Fact]
    public void KeHoach_Exit_KhongCoEntrySlTpVaKhongHuy()
    {
        var keHoach = Tao().TinhKeHoach(BreakdownBars(), 22m);

        Assert.Equal(0m, keHoach.GiaVaoLenhMin);
        Assert.Equal(0m, keHoach.GiaVaoLenhMax);
        Assert.Equal(0m, keHoach.GiaDungLo);
        Assert.Equal(0m, keHoach.GiaChotLoi1);
        Assert.Equal(0m, keHoach.GiaChotLoi2);
        Assert.Contains("KHÔNG CÓ", keHoach.DieuKienHuy);
    }

    [Fact]
    public void ThieuLichSu_HinhThaiVaTrigger_KhongDat()
    {
        var bars = TangDan(n: 30);

        var kichBan = Tao();
        Assert.True(kichBan.DanhGiaBoiCanh(bars).Dat); // bối cảnh luôn đạt
        Assert.False(kichBan.DanhGiaHinhThai(bars).Dat);
        Assert.False(kichBan.KiemTraCoKichHoat(bars, 100m, 1_000_000).Dat);
    }
}
