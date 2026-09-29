using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using Xunit;

namespace StockRadar.Tests.KichBan;

/// <summary>
/// Kiểm thử kịch bản "Quét thanh khoản" (Liquidity Sweep — BUY).
/// Dữ liệu giả lập tất định: nền phẳng bị quét đáy rồi giành lại.
/// </summary>
public sealed class KichBanQuetThanhKhoanTests
{
    private static KichBanQuetThanhKhoan Tao() => new(new KichBanQuetThanhKhoanOptions());

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

    /// <summary>
    /// Nền phẳng 25 rồi phiên cuối quét sâu xuống (Low 23.0 &lt; đáy nền 24.875) và đóng cửa giành lại.
    /// </summary>
    private static List<OhlcvBar> SweepBars(bool coQuet = true, long vol = 1_000_000)
    {
        var closes = Enumerable.Repeat(25m, 250).ToList();
        var vols = Enumerable.Repeat(vol, 250).ToList();
        var bars = BarsFrom(closes, vols);
        // Thay phiên cuối bằng nến quét (râu dưới sâu) rồi đóng cửa gần đáy nền.
        var d = new DateOnly(2025, 1, 1).AddDays(249);
        var low = coQuet ? 23.0m : 24.875m;
        bars[249] = new OhlcvBar(d, 25m, 25.2m, low, 24.9m, vol);
        return bars;
    }

    // ===== Metadata =====

    [Fact]
    public void Metadata_LoaiKichBanVaKhongPhaiBan()
    {
        var kichBan = Tao();
        Assert.Equal(LoaiKichBan.QuetThanhKhoan, kichBan.LoaiKichBan);
        Assert.False(kichBan.LaKichBanBan);
    }

    // ===== Bối cảnh =====

    [Fact]
    public void BoiCanh_Dat_KhiUptrend()
    {
        var ketQua = Tao().DanhGiaBoiCanh(TangDan());

        Assert.True(ketQua.Dat);
        Assert.True(ketQua.BangChungs[0].Dat); // EMA20 > EMA50
        Assert.True(ketQua.BangChungs[1].Dat); // Close > EMA50
    }

    [Fact]
    public void BoiCanh_KhongDat_KhiGiamTuDo()
    {
        var ketQua = Tao().DanhGiaBoiCanh(GiamDan());

        Assert.False(ketQua.Dat);
        Assert.False(ketQua.BangChungs[1].Dat); // Close < EMA50
    }

    // ===== Hình thái =====

    [Fact]
    public void HinhThai_Dat_KhiLowXuyenDayNen()
    {
        var bars = SweepBars(coQuet: true);

        var ketQua = Tao().DanhGiaHinhThai(bars);

        Assert.True(ketQua.Dat);
        Assert.True(ketQua.BangChungs[0].Dat); // có đáy nền
        Assert.True(ketQua.BangChungs[1].Dat); // Low xuyên đáy nền
    }

    [Fact]
    public void HinhThai_KhongDat_KhiKhongXuyenDayNen()
    {
        var bars = SweepBars(coQuet: false);

        var ketQua = Tao().DanhGiaHinhThai(bars);

        Assert.False(ketQua.Dat);
        Assert.False(ketQua.BangChungs[1].Dat);
    }

    // ===== Cò kích hoạt =====

    [Fact]
    public void Trigger_Dat_KhiReclaimVaVolumeManh()
    {
        var bars = SweepBars(coQuet: true);
        // Đáy nền ≈ 24.875 → ngưỡng reclaim ≈ 25.0; giá 26 giành lại, volume 2.5×.
        var ketQua = Tao().KiemTraCoKichHoat(bars, giaHienTai: 26m, volumeHienTai: 2_500_000);

        Assert.True(ketQua.Dat);
        Assert.True(ketQua.BangChungs[0].Dat); // reclaim
        Assert.True(ketQua.BangChungs[1].Dat); // volume spike
    }

    [Fact]
    public void Trigger_KhongDat_KhiVolumeYeu()
    {
        var bars = SweepBars(coQuet: true);

        var ketQua = Tao().KiemTraCoKichHoat(bars, giaHienTai: 26m, volumeHienTai: 1_200_000);

        Assert.False(ketQua.Dat);
        Assert.False(ketQua.BangChungs[1].Dat);
    }

    // ===== Kế hoạch =====

    [Fact]
    public void KeHoach_TinhDungEntrySlTp()
    {
        var bars = SweepBars(coQuet: true);
        var keHoach = Tao().TinhKeHoach(bars, giaHienTai: 26m);

        var lowQuet = bars.TakeLast(5).Min(b => b.Low);
        var dinhHop = bars.TakeLast(20).Max(b => b.High);
        var atr = IndicatorMath.Atr(bars, 14);

        Assert.Equal(26m * 0.997m, keHoach.GiaVaoLenhMin);
        Assert.Equal(26m * 1.003m, keHoach.GiaVaoLenhMax);
        Assert.Equal(lowQuet - 0.3m * atr, keHoach.GiaDungLo);
        Assert.Equal(dinhHop, keHoach.GiaChotLoi1);
        Assert.Equal(dinhHop + (dinhHop - 26m * 0.997m) * 0.5m, keHoach.GiaChotLoi2);
        Assert.Contains("đáy quét", keHoach.DieuKienHuy);
    }

    [Fact]
    public void ThieuLichSu_CacVaiTro_KhongDat()
    {
        var bars = TangDan(n: 30);

        var kichBan = Tao();
        Assert.False(kichBan.DanhGiaBoiCanh(bars).Dat);
        Assert.False(kichBan.DanhGiaHinhThai(bars).Dat);
        Assert.False(kichBan.KiemTraCoKichHoat(bars, 100m, 1_000_000).Dat);
    }
}
