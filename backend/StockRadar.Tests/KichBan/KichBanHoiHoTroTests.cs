using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using Xunit;

namespace StockRadar.Tests.KichBan;

/// <summary>
/// Kiểm thử kịch bản "Hồi về hỗ trợ" (Pullback — BUY): Bối cảnh, Hình thái, Cò kích hoạt, Kế hoạch.
/// Dữ liệu là các dãy nến giả lập dựng để điều khiển từng điều kiện một cách tất định.
/// </summary>
public sealed class KichBanHoiHoTroTests
{
    private static KichBanHoiHoTro Tao() => new(new KichBanHoiHoTroOptions());

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

    /// <summary>Tăng đều nhẹ — EMA20 &gt; EMA50 &gt; EMA200.</summary>
    private static List<OhlcvBar> TangDan(int n = 250, decimal start = 20m, decimal step = 0.03m, long vol = 1_000_000)
    {
        var closes = Enumerable.Range(0, n).Select(i => start + i * step).ToList();
        var vols = Enumerable.Repeat(vol, n).ToList();
        return BarsFrom(closes, vols);
    }

    /// <summary>Giảm đều — EMA20 &lt; EMA50 &lt; EMA200.</summary>
    private static List<OhlcvBar> GiamDan(int n = 250, decimal start = 40m, decimal step = 0.03m, long vol = 1_000_000)
    {
        var closes = Enumerable.Range(0, n).Select(i => start - i * step).ToList();
        var vols = Enumerable.Repeat(vol, n).ToList();
        return BarsFrom(closes, vols);
    }

    /// <summary>
    /// Uptrend nhẹ rồi pullback dao động (RSI ≈ 43), volume 3 phiên cuối teo — dùng cho Hình thái ĐẠT.
    /// </summary>
    private static List<OhlcvBar> PullbackBars(long vol = 1_000_000)
    {
        var closes = new List<decimal>();
        var vols = new List<long>();
        for (var i = 0; i < 235; i++) { closes.Add(20m + i * 0.03m); vols.Add(vol); }
        var gia = closes[^1];
        // 14 thay đổi: −0.2 / +0.15 xen kẽ → RS ≈ 0.75 → RSI ≈ 42.9 (trong [40,50]).
        for (var k = 0; k < 14; k++)
        {
            gia += k % 2 == 0 ? -0.2m : +0.15m;
            closes.Add(gia);
            vols.Add(vol);
        }
        // 3 phiên cuối volume teo còn 50%.
        for (var i = closes.Count - 3; i < closes.Count; i++) vols[i] = vol / 2;
        return BarsFrom(closes, vols);
    }

    // ===== Bối cảnh =====

    [Fact]
    public void BoiCanh_Dat_KhiEmaXepHangTang()
    {
        var bars = TangDan();

        var ketQua = Tao().DanhGiaBoiCanh(bars);

        Assert.True(ketQua.Dat);
        Assert.Equal(2, ketQua.BangChungs.Count);
        Assert.True(ketQua.BangChungs[0].Dat); // EMA20 > EMA50
        Assert.True(ketQua.BangChungs[1].Dat); // bonus: EMA50 > EMA200
        Assert.All(ketQua.BangChungs, b => Assert.Equal(VaiTroChiBao.BoiCanh, b.VaiTro));
    }

    [Fact]
    public void BoiCanh_Dat_KhiThieu200Phien_BoQuaEma200()
    {
        // Chỉ 120 phiên: EMA200 không khả dụng → bỏ qua hoàn toàn, không fail.
        var bars = TangDan(n: 120);

        var ketQua = Tao().DanhGiaBoiCanh(bars);

        Assert.True(ketQua.Dat);
        Assert.Single(ketQua.BangChungs); // không có bằng chứng EMA200
        Assert.True(ketQua.BangChungs[0].Dat); // EMA20 > EMA50
    }

    [Fact]
    public void BoiCanh_Ema200ChiLaBonus_KhongAnhHuongKetQua()
    {
        // Đủ 250 phiên nhưng EMA50 < EMA200 (hồi mạnh trong downtrend dài) → bằng chứng bonus fail,
        // kết quả vẫn ĐẠT vì EMA20 > EMA50 là điều kiện bắt buộc duy nhất.
        // Dãy này cho EMA20 ≈ 59.78 > EMA50 ≈ 53.62 nhưng EMA50 < EMA200 ≈ 63.53.
        var closes = new List<decimal>();
        for (var i = 0; i < 220; i++) closes.Add(100m - i * 0.28m); // downtrend dài → EMA200 cao
        var gia = closes[^1];
        for (var k = 0; k < 30; k++) { gia += 1.0m; closes.Add(gia); } // hồi mạnh ngắn hạn
        var bars = BarsFrom(closes, Enumerable.Repeat(1_000_000L, closes.Count).ToList());

        var ketQua = Tao().DanhGiaBoiCanh(bars);

        Assert.Equal(2, ketQua.BangChungs.Count);
        Assert.True(ketQua.BangChungs[0].Dat);   // EMA20 > EMA50 (bắt buộc)
        Assert.False(ketQua.BangChungs[1].Dat);  // EMA50 < EMA200 (bonus)
        Assert.True(ketQua.Dat);                 // bonus không có quyền veto
    }

    [Fact]
    public void BoiCanh_KhongDat_KhiGiamDan()
    {
        var bars = GiamDan();

        var ketQua = Tao().DanhGiaBoiCanh(bars);

        Assert.False(ketQua.Dat);
        Assert.False(ketQua.BangChungs[0].Dat);
    }

    // ===== Hình thái =====

    [Fact]
    public void HinhThai_Dat_KhiGanEma20RsiDieuChinhVolumeTeo()
    {
        var bars = PullbackBars();

        var ketQua = Tao().DanhGiaHinhThai(bars);

        Assert.True(ketQua.Dat);
        Assert.True(ketQua.BangChungs[0].Dat); // gần EMA20
        Assert.True(ketQua.BangChungs[1].Dat); // RSI 40–50
        Assert.True(ketQua.BangChungs[2].Dat); // volume bán teo
        Assert.All(ketQua.BangChungs, b => Assert.Equal(VaiTroChiBao.HinhThai, b.VaiTro));
    }

    [Fact]
    public void HinhThai_KhongDat_KhiRsiQuaCao()
    {
        // Tăng nóng liên tục → RSI ≈ 100, ngoài vùng điều chỉnh [40,50].
        var bars = TangDan(step: 0.2m);

        var ketQua = Tao().DanhGiaHinhThai(bars);

        Assert.False(ketQua.Dat);
        Assert.False(ketQua.BangChungs[1].Dat);
    }

    // ===== Cò kích hoạt =====

    [Fact]
    public void Trigger_Dat_KhiRsiBatLenVaMacdTangVaReclaim()
    {
        var bars = PullbackBars();
        var ema20 = IndicatorMath.Ema(bars, 20);
        var giaHienTai = ema20 + 0.5m; // đã reclaim trên EMA20

        var ketQua = Tao().KiemTraCoKichHoat(bars, giaHienTai, 1_000_000);

        // Phiên cuối của PullbackBars là phiên bật lên (+0.15) → RSI & MACD hist tăng.
        Assert.True(ketQua.BangChungs[2].Dat); // reclaim EMA20
        Assert.Equal(VaiTroChiBao.CoKichHoat, ketQua.BangChungs[0].VaiTro);
    }

    [Fact]
    public void Trigger_KhongDat_KhiGiaDuoiEma20()
    {
        var bars = GiamDan();
        var ema20 = IndicatorMath.Ema(bars, 20);
        var giaHienTai = ema20 - 1.0m; // dưới EMA20

        var ketQua = Tao().KiemTraCoKichHoat(bars, giaHienTai, 1_000_000);

        Assert.False(ketQua.Dat);
        Assert.False(ketQua.BangChungs[2].Dat); // không reclaim
    }

    // ===== Kế hoạch =====

    [Fact]
    public void KeHoach_TinhDungEntrySlTp()
    {
        var bars = PullbackBars();
        var keHoach = Tao().TinhKeHoach(bars, bars[^1].Close);

        var ema20 = IndicatorMath.Ema(bars, 20);
        var ema50 = IndicatorMath.Ema(bars, 50);
        var atr = IndicatorMath.Atr(bars, 14);
        var dinh20 = bars.TakeLast(20).Max(b => b.High);

        Assert.Equal(ema20 * 0.995m, keHoach.GiaVaoLenhMin);
        Assert.Equal(ema20 * 1.005m, keHoach.GiaVaoLenhMax);
        Assert.Equal(ema50 - 0.5m * atr, keHoach.GiaDungLo);
        Assert.Equal(dinh20, keHoach.GiaChotLoi1);
        Assert.Equal(dinh20 + (dinh20 - ema20 * 0.995m) * 0.5m, keHoach.GiaChotLoi2);
        Assert.Contains("EMA50", keHoach.DieuKienHuy);
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
