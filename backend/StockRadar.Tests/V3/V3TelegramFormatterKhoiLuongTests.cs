using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.V3;

/// <summary>
/// Tin MUA V3 phải ghi số cổ phiếu và tỉ trọng thực sau làm tròn lô, tiền theo triệu đồng.
/// Dữ liệu bịa có kiểm soát: vốn 40 triệu, KB4 giá 53,2, 200 cp.
/// </summary>
public sealed class V3TelegramFormatterKhoiLuongTests
{
    /// <summary>Tạo tin MUA KB4 vốn 40 triệu, 200 cp giá 53,2.</summary>
    /// <param name="moCuaPhienSau">true = tin ATO lúc 08:30, false = tin ATC đã khớp.</param>
    /// <returns>Nội dung tin HTML.</returns>
    private static string Mua(bool moCuaPhienSau) =>
        V3TelegramFormatter.FormatMua(
            LoaiKichBanV3.KB4, "FPT", 53.2m, 120m, -12m, 1, 5,
            breadth50: 0.3m, chiSoDoLechMa50: -2m, heSoTrongSo: 1m,
            khoiLuong: 200m, giaTriLenh: 10640m, giaTriDanhMuc: 40000m,
            moCuaPhienSau: moCuaPhienSau, giuToiDa: 15, ngayAtoN3: null, cfg: new V3CauHinh());

    [Fact]
    public void Tin_ATC_ghi_so_co_phieu_gia_tri_va_ty_trong_thuc()
    {
        var msg = Mua(moCuaPhienSau: false);

        Assert.Contains("Quy mô: <code>20%</code> DANH MỤC (8 triệu — V/5 × 1)", msg);
        Assert.Contains("Khối lượng: <code>200 cp</code> = 10.64 triệu (26.6% DM)", msg);
        Assert.DoesNotContain("tỷ —", msg);
    }

    [Fact]
    public void Tin_ATO_ghi_khoi_luong_la_uoc_tinh()
    {
        var msg = Mua(moCuaPhienSau: true);

        Assert.Contains("Khối lượng ước tính: <code>200 cp</code> ≈ 10.64 triệu (26.6% DM) — chốt lại theo giá khớp ATO", msg);
    }
}
