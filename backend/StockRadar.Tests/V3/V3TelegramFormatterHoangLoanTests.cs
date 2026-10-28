using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.V3;

/// <summary>
/// SC-008 (Phụ lục E): dòng tin MUA hoảng loạn sâu phải lấy ngưỡng 0,15/+15% TỪ CẤU HÌNH (FR-011),
/// không in cứng — đổi cấu hình mà tin vẫn in số cũ thì tin sai mà không ai phát hiện.
/// Dữ liệu bịa có kiểm soát (mã "HPG" chỉ là ví dụ minh họa).
/// </summary>
public sealed class V3TelegramFormatterHoangLoanTests
{
    private static string Mua(LoaiKichBanV3 loai, V3CauHinh cfg, bool laHoangLoanSau) =>
        V3TelegramFormatter.FormatMua(
            loai, "HPG", 27.3m, 38m, 4m, 1, 5,
            breadth50: 0.12m, chiSoDoLechMa50: -8.1m, heSoTrongSo: 1.5m, giaTriLenhTyDong: 0.3m,
            moCuaPhienSau: true,
            V3LuatBanThucTe.GiuToiDaTheoViThe(cfg, loai, laHoangLoanSau),
            ngayAtoN3: null, cfg: cfg, laHoangLoanSau: laHoangLoanSau);

    [Fact]
    public void Co_bat_in_nguong_breadth_va_chot_loi_theu_cau_hinh()
    {
        // Cấu hình KHÁC mặc định (0,25 · +10%) — chứng minh số in ra đến từ cfg, không phải hằng.
        var cfg = new V3CauHinh { HoangLoanSauBreadth50Max = 0.25m, HoangLoanSauChotLoi = 1.1m };

        var msg = Mua(LoaiKichBanV3.KB1, cfg, laHoangLoanSau: true);

        Assert.Contains("Hoảng loạn sâu", msg);
        Assert.Contains("0.25", msg);            // ngưỡng breadth lấy từ cfg (in invariant "0.25")
        Assert.Contains("giá vào +10%", msg);    // 1,1 → +10%, không phải "+15%" cứng
    }

    [Fact]
    public void Co_tat_thi_in_dong_cham_ma_nhu_lenh_thuong()
    {
        var cfg = new V3CauHinh();

        var msg = Mua(LoaiKichBanV3.KB1, cfg, laHoangLoanSau: false);

        Assert.DoesNotContain("Hoảng loạn sâu", msg);
        Assert.Contains("max(chạm MA10", msg);   // hành vi lệnh thường giữ nguyên
    }
}
