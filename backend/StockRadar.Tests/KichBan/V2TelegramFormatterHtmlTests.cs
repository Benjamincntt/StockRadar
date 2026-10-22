using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.KichBan;

/// <summary>
/// Hồi quy hiển thị tin Telegram V2: giờ in ra phải là giờ Việt Nam và
/// ký tự thô &lt; &gt; &amp; trong dữ liệu phải được escape (HTML parse mode).
/// Dữ liệu trong test là giả.
/// </summary>
public sealed class V2TelegramFormatterHtmlTests
{
    private static KetQuaKichBan ViThiMua(
        DateTime kichHoatUtc,
        BangChung? bangChung = null) => new()
    {
        Symbol = "HPG",
        LoaiKichBan = LoaiKichBan.NoHuongLen,
        TrangThai = TrangThaiKichBan.DaKichHoat,
        DatBoiCanh = true,
        DatHinhThai = true,
        DatCoKichHoat = true,
        ThoiGianKichHoat = kichHoatUtc,
        GiaKhop = 10.5m,
        DanhSachBangChung = bangChung is null
            ? new List<BangChung>()
            : new List<BangChung> { bangChung },
        KeHoach = new KeHoachGiaoDich
        {
            GiaVaoLenhMin = 10m,
            GiaVaoLenhMax = 11m,
            GiaDungLo = 9.5m,
            GiaChotLoi1 = 12m,
            GiaChotLoi2 = 14m,
            DieuKienHuy = "Đóng cửa dưới 9.5",
        },
    };

    [Fact]
    public void FormatMua_GioKichHoatUTC_InRaGioVN()
    {
        // 02:00 UTC = 09:00 VN (VN = UTC+7, không DST).
        var tin = V2TelegramFormatter.FormatMua(
            ViThiMua(new DateTime(2026, 10, 19, 2, 0, 0, DateTimeKind.Utc)));

        Assert.Contains("Kích hoạt lúc: 09:00 (VN)", tin);
        Assert.DoesNotContain("02:00", tin);
    }

    [Fact]
    public void FormatMua_BangChungChuaKyTuTho_EscapeHtmlKhongMatNoiDung()
    {
        // Dữ liệu thật từ KichBanQuetThanhKhoan/HoiHoTro chứa '>' và '<' thô.
        var tin = V2TelegramFormatter.FormatMua(
            ViThiMua(
                new DateTime(2026, 10, 19, 2, 0, 0, DateTimeKind.Utc),
                new BangChung
                {
                    MoTa = "Uptrend (EMA20 > EMA50) hoặc sideways tích lũy (Bollinger hẹp)",
                    GiaTriThucTe = "< 60 phiên",
                    Dat = true,
                }));

        Assert.Contains("EMA20 &gt; EMA50", tin);
        Assert.Contains("&lt; 60 phiên", tin);
    }

    [Fact]
    public void FormatBanNua_LyDoDaCoTheCode_KhongBiDoubleEscape()
    {
        // Runner cố tình truyền <code> trong lyDo — escape phải giữ nguyên thẻ hợp lệ.
        var tin = V2TelegramFormatter.FormatBanNua(
            "HPG", "Rút mốc 4%", 11.5m, 9.5m, "Rút mốc 4%: mốc <code>12</code> giảm ≥ 4%");

        Assert.Contains("mốc <code>12</code>", tin);
        Assert.DoesNotContain("&lt;code&gt;", tin);
    }
}
