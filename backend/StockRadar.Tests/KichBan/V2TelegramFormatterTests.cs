using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.KichBan;

/// <summary>
/// T017 (spec 009 US2): tin mua phải in đủ các mục theo contracts/telegram-mua.md,
/// % tính từ giá vào thực tế, dòng dừng lỗ đuổi chỉ hiện khi kế hoạch bật đuổi.
/// Dữ liệu trong test là giả.
/// </summary>
public sealed class V2TelegramFormatterTests
{
    private static KetQuaKichBan ViThiMua(bool coDuoi, bool doiDungLo) => new()
    {
        Symbol = "HPG",
        LoaiKichBan = LoaiKichBan.NoHuongLen,
        TrangThai = TrangThaiKichBan.DaKichHoat,
        DatBoiCanh = true,
        DatHinhThai = true,
        DatCoKichHoat = true,
        ThoiGianKichHoat = new DateTime(2026, 10, 19, 3, 0, 0, DateTimeKind.Utc),
        // Giá khớp 10.5 nằm trong vùng 10–11 — % phải tính từ 10.5, không từ biên dưới.
        GiaKhop = 10.5m,
        KeHoach = new KeHoachGiaoDich
        {
            GiaVaoLenhMin = 10m,
            GiaVaoLenhMax = 11m,
            GiaDungLo = 9.5m,
            GiaChotLoi1 = 12m,
            GiaChotLoi2 = 14m,
            TyLeBanChotLoi1 = 0.5m,
            DoiDungLoVeGiaVao = doiDungLo,
            CoDungLoDuoi = coDuoi,
            KhoangCachDuoi = coDuoi ? 0.55m : null,
            SoPhienDungTheoThoiGian = 5,
            NguongLaiToiThieuPhanTram = 3m,
            SoPhienToiDa = 20,
        },
    };

    [Fact]
    public void FormatMua_TheoKeHoachDuInDua_TuGiaVaoThucTe()
    {
        var tin = V2TelegramFormatter.FormatMua(ViThiMua(coDuoi: true, doiDungLo: true));

        // Header + giá vào thực tế kèm vùng vào.
        Assert.Contains("HPG", tin);
        Assert.Contains("· MUA", tin);
        Assert.Contains("Giá vào: <code>10.5</code>", tin);
        Assert.Contains("(vùng <code>10</code>–<code>11</code>)", tin);

        // % tính từ 10.5: SL 9.5 → −9.5%, CL1 12 → +14.3%, CL2 14 → +33.3%.
        Assert.Contains("Cắt lỗ: <code>9.5</code> (-9.5%)", tin);
        Assert.Contains("Chốt lời 1: <code>12</code> (+14.3%) — bán 50%, sau đó dời cắt lỗ về giá vào", tin);
        Assert.Contains("Chốt lời 2: <code>14</code> (+33.3%) — bán phần còn lại", tin);

        // Đuổi theo chỉ hiện khi CoDungLoDuoi, kèm khoảng cách chốt.
        Assert.Contains("Dừng lỗ đuổi theo: đỉnh − <code>0.55</code>", tin);

        // Quy tắc thời gian đọc từ kế hoạch + lãi/lỗ kỳ vọng + ghi chú T+2.5.
        Assert.Contains("Thời gian: sau 5 phiên chưa lãi 3% → bán; tối đa 20 phiên", tin);
        Assert.Contains("Lãi/lỗ kỳ vọng:", tin);
        Assert.Contains("Chưa bán được trước T+2.5", tin);
    }

    [Fact]
    public void FormatMua_KhongDuoi_KhongDoiCatLo_ThuongXuyenKhai()
    {
        var tin = V2TelegramFormatter.FormatMua(ViThiMua(coDuoi: false, doiDungLo: false));

        Assert.DoesNotContain("Dừng lỗ đuổi theo", tin);
        Assert.DoesNotContain("dời cắt lỗ về giá vào", tin);
        // Các mục bắt buộc vẫn đủ.
        Assert.Contains("Giá vào: <code>10.5</code>", tin);
        Assert.Contains("bán 50%", tin);
        Assert.Contains("Chưa bán được trước T+2.5", tin);
    }

    [Fact]
    public void FormatMua_TyLeLaiLoTinhTuBienTrenVungVao()
    {
        // (12 − 11) / (11 − 9.5) = 0.6667 → in "0.7" theo F1.
        var tin = V2TelegramFormatter.FormatMua(ViThiMua(coDuoi: false, doiDungLo: false));
        Assert.Contains("Lãi/lỗ kỳ vọng: 0.7", tin);
    }
}
