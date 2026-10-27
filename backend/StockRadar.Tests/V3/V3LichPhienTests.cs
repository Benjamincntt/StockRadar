using StockRadar.Domain.Services;

namespace StockRadar.Tests.V3;

/// <summary>
/// FR-017 (T+2.5): đúng phiên T+2 chỉ được bán từ đầu buổi chiều (13:00),
/// sáng phiên T+2 hàng chưa về tài khoản → CẤM bán. Từ phiên T+3 bán cả ngày.
/// </summary>
public sealed class V3LichPhienTests
{
    private static readonly TimeOnly MocPhieuChieu = new(13, 0);
    private const int Tplus25 = 2;

    [Theory]
    [InlineData(0, "09:30", false)] // T+0 (phiên mua)
    [InlineData(1, "13:30", false)] // T+1 sáng/chiều đều chưa được bán
    [InlineData(2, "09:00", false)] // SÁNG phiên T+2 → chưa
    [InlineData(2, "12:59", false)] // sát mốc nhưng vẫn sáng
    [InlineData(2, "13:00", true)]  // CHIỀU phiên T+2 → đúng mốc 13:00 được bán
    [InlineData(2, "14:30", true)]
    [InlineData(3, "09:05", true)]  // từ T+3 bán cả ngày
    [InlineData(15, "09:00", true)]
    public void Dung_phien_tplus2_chi_ban_tu_13_gio(int soPhienGiua, string gioXet, bool kyVongBanDuoc)
    {
        var dc = V3LichPhien.DaToiTplus25(
            soPhienGiua, TimeOnly.Parse(gioXet), Tplus25, MocPhieuChieu);

        Assert.Equal(kyVongBanDuoc, dc);
    }

    [Fact]
    public void Mua_thu_22_10_sang_thu_hai_26_10_chua_ban_duoc_chieu_26_10_thi_duoc()
    {
        // Lịch phiên thật: 22/10 (T) → 23/10 (T+1) → 26/10 (T+2, thứ Hai sau cuối tuần).
        var ngayMua = new DateOnly(2026, 10, 22);
        var sangT2 = new DateOnly(2026, 10, 26);

        var soPhien = TradingSessionMath.TradingSessionsBetween(ngayMua, sangT2);
        Assert.Equal(2, soPhien); // đúng phiên T+2

        Assert.False(V3LichPhien.DaToiTplus25(soPhien, new TimeOnly(9, 40), Tplus25, MocPhieuChieu));
        Assert.True(V3LichPhien.DaToiTplus25(soPhien, new TimeOnly(13, 30), Tplus25, MocPhieuChieu));

        // Sang T+3 (27/10) thì buổi sáng đã bán được.
        var soPhienT3 = TradingSessionMath.TradingSessionsBetween(ngayMua, new DateOnly(2026, 10, 27));
        Assert.True(V3LichPhien.DaToiTplus25(soPhienT3, new TimeOnly(9, 5), Tplus25, MocPhieuChieu));
    }

    [Fact]
    public void Moc_buoi_chieu_doc_tu_cau_hinh_khong_hardcode()
    {
        // Owner đổi mốc sang 13:30 qua appsettings → 13:00 không còn được bán ở phiên T+2.
        var cfg = new V3CauHinh { MocPhieuChieuTplus2 = new TimeOnly(13, 30) };

        Assert.False(V3LichPhien.DaToiTplus25(2, new TimeOnly(13, 0), cfg.Tplus25Sessions, cfg.MocPhieuChieuTplus2));
        Assert.True(V3LichPhien.DaToiTplus25(2, new TimeOnly(13, 30), cfg.Tplus25Sessions, cfg.MocPhieuChieuTplus2));
    }
}
