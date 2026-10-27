using StockRadar.Domain.Services;

namespace StockRadar.Tests.V3;

/// <summary>
/// SC-002 — chấm kết quả lệnh V3 đã đóng (Phụ lục H / FR-024):
/// lãi ròng = (giá thoát/giá vào − 1) − phí khứ hồi; Thắng = lãi ròng &gt; 0,05%;
/// trong ±ngưỡng là HOÀ VỐN (không đếm thắng) — sửa đúng lỗi "hoà vốn bị đếm thắng".
/// </summary>
public sealed class V3ChamKetQuaTests
{
    private static readonly V3CauHinh Cfg = new();

    [Fact]
    public void Lai_rong_tru_phi_khu_hoi_mot_lan()
    {
        // 100 → 110 = +10%, trừ phí khứ hồi 0,4% còn +9,6%.
        Assert.Equal(0.096m, V3ChamKetQua.LaiRong(100m, 110m, Cfg));
    }

    [Fact]
    public void Gia_khong_hop_le_thi_lai_rong_bang_0()
    {
        Assert.Equal(0m, V3ChamKetQua.LaiRong(0m, 110m, Cfg));
        Assert.Equal(0m, V3ChamKetQua.LaiRong(100m, 0m, Cfg));
    }

    [Theory]
    [InlineData(0.0006d, "Thang")]   // +0,06% > ngưỡng 0,05%
    [InlineData(0.0005d, "HoaVon")]  // đúng ngưỡng → KHÔNG thắng (so sánh >)
    [InlineData(0d, "HoaVon")]
    [InlineData(-0.0005d, "HoaVon")] // -0,05% vẫn trong vùng hòa vốn
    [InlineData(-0.0006d, "Thua")]   // -0,06% < −ngưỡng → thua
    public void Xep_loai_theo_nguong_005_phan_tram(double laiRong, string expected)
    {
        Assert.Equal(
            Enum.Parse<V3ChamKetQua.KetQua>(expected),
            V3ChamKetQua.XepLoai((decimal)laiRong, Cfg));
    }

    [Fact]
    public void Cham_lo_nhe_trong_vi_hoa_von_khong_dem_thang()
    {
        // 100 → 100,35: +0,35% − 0,4% phí = −0,05% — nằm đúng biên ngưỡng thắng 0,05% → HÒA VỐN,
        // không được đếm thắng cũng không đếm thua (lỗi "hoà vốn bị đếm thắng" của research 01 dòng 73).
        var (lai, xepLoai) = V3ChamKetQua.Cham(100m, 100.35m, Cfg);

        Assert.Equal(-0.0005m, lai);
        Assert.Equal(V3ChamKetQua.KetQua.HoaVon, xepLoai);
    }

    [Fact]
    public void Cham_thang_khi_lai_gross_du_lon_hon_nguong()
    {
        // 100 → 101: +1% − 0,4% phí = +0,6% > 0,05% → Thắng.
        var (lai, xepLoai) = V3ChamKetQua.Cham(100m, 101m, Cfg);

        Assert.Equal(0.006m, lai);
        Assert.Equal(V3ChamKetQua.KetQua.Thang, xepLoai);
    }
}
