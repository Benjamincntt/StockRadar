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

    // ===== Phase 7 — điểm mua 2 trong tin mua điểm 1 + tin mua thêm (dữ liệu giả) =====

    /// <summary>Kế hoạch có điểm mua 2 (11.5 &lt; CL1 12, band 0.3) — dữ liệu giả.</summary>
    private static KetQuaKichBan ViThiMuaCoDiem2() => new()
    {
        Symbol = "HPG",
        LoaiKichBan = LoaiKichBan.NoHuongLen,
        TrangThai = TrangThaiKichBan.DaKichHoat,
        DatBoiCanh = true,
        DatHinhThai = true,
        DatCoKichHoat = true,
        ThoiGianKichHoat = new DateTime(2026, 10, 19, 3, 0, 0, DateTimeKind.Utc),
        GiaKhop = 10.5m,
        KeHoach = new KeHoachGiaoDich
        {
            GiaVaoLenhMin = 10m,
            GiaVaoLenhMax = 11m,
            GiaDungLo = 9.5m,
            GiaChotLoi1 = 12m,
            GiaChotLoi2 = 14m,
            GiaMuaDiem2 = 11.5m,
            BandRongDiem2 = 0.3m,
        },
    };

    [Fact]
    public void FormatMua_CoDiem2_InDongDiemMua2VaRutTuMoc()
    {
        var tin = V2TelegramFormatter.FormatMua(ViThiMuaCoDiem2());
        // Điểm 2 chỉ in khi kế hoạch có điểm 2 (C-9).
        Assert.Contains("Điểm mua 2", tin);
        Assert.Contains("<code>11.5</code>", tin);
        // Dòng rút mốc luôn in cho mọi vị thế (C-12).
        Assert.Contains("Rút từ mốc", tin);
        Assert.Contains("−4%", tin);
        Assert.Contains("−6%", tin);
    }

    [Fact]
    public void FormatMua_KhongDiem2_InDongKhong()
    {
        var tin = V2TelegramFormatter.FormatMua(ViThiMua(coDuoi: false, doiDungLo: false));
        // Không có điểm 2 → in ngắn "không" (format chưa chủ sản phẩm duyệt chi tiết).
        Assert.Contains("Điểm mua 2: không", tin);
    }

    [Fact]
    public void FormatMuaDiem2_InBinhQuanVaBoChotLoi()
    {
        // giá 1 = 10.5, khớp điểm 2 = 11.5 → bình quân 11 (dữ liệu giả).
        var tin = V2TelegramFormatter.FormatMuaDiem2("HPG", "NỔ HƯỚNG LÊN", 10.5m, 11.5m, 11m, 12m, 9.5m);
        Assert.Contains("HPG", tin);
        Assert.Contains("MUA THÊM", tin);
        Assert.Contains("điểm 2", tin);
        Assert.Contains("Bình quân", tin);
        Assert.Contains("<code>11</code>", tin);
        Assert.Contains("Bỏ chốt lời 1/2", tin);
        // C-30: nội dung tin không chứa mã quyết định nội bộ.
        Assert.DoesNotContain("(C-", tin);
    }

    // ===== Phase 8 — tin rút mốc 4% / 6% theo contracts/telegram-mua.md (dữ liệu giả) =====

    [Fact]
    public void FormatBanNua_RutMoc_TheoContract_InTieuDeLyDoVaCatConLai()
    {
        // Mốc 12, giá khớp 11.5 (−4.2%), cắt còn lại 9.5.
        var tin = V2TelegramFormatter.FormatBanNua(
            "HPG", "Rút mốc 4%", 11.5m, 9.5m, "Rút mốc 4%: mốc <code>12</code> giảm ≥ 4%");

        Assert.Contains("HPG", tin);
        Assert.Contains("BÁN NỬA (Rút mốc 4%)", tin);
        Assert.Contains("Giá: <code>11.5</code>", tin);
        Assert.Contains("mốc <code>12</code>", tin);
        Assert.Contains("giảm ≥ 4%", tin);
        Assert.Contains("Cắt còn lại: <code>9.5</code>", tin);
        // Bán nửa không phải lệnh thoát hết → header không dùng chữ BÁN HẾT.
        Assert.DoesNotContain("BÁN HẾT", tin);
        Assert.DoesNotContain("(C-", tin);
    }

    [Fact]
    public void FormatBanHetRutMoc_TheoContract_InLyDoVaDongBinhQuan()
    {
        // Mốc 12, giá khớp 11.2 (−6.7%), bình quân 10.5 → lãi +6.7%.
        var tin = V2TelegramFormatter.FormatBanHetRutMoc("HPG", 11.2m, 10.5m, 12m);

        Assert.Contains("HPG", tin);
        Assert.Contains("BÁN HẾT (RutMoc6)", tin);
        Assert.Contains("Giá: <code>11.2</code>", tin);
        Assert.Contains("Lý do: RutMoc6", tin);
        Assert.Contains("so bình quân <code>10.5</code>", tin);
        Assert.Contains("Chu kỳ đóng", tin);
        Assert.DoesNotContain("(C-", tin);
    }

    // ===== Phase 9 (C-29/C-30) — CL1 dùng chung format bán nửa; tin không còn mã quyết định =====

    [Fact]
    public void FormatBanNua_ChotLoi1_DungCauTrucGiongTinRutMoc()
    {
        // CL1: giá 12.05, mục tiêu 12, cắt dời về giá vào 10.5 (dữ liệu giả).
        var tinCl1 = V2TelegramFormatter.FormatBanNua(
            "HPG", "Chốt lời 1", 12.05m, 10.5m, "Chốt lời 1: chạm mục tiêu <code>12</code>");
        var tinRut = V2TelegramFormatter.FormatBanNua(
            "HPG", "Rút mốc 4%", 11.5m, 9.5m, "Rút mốc 4%: mốc <code>12</code> giảm ≥ 4%");

        Assert.Contains("🔻 <b>HPG</b> — BÁN NỬA (Chốt lời 1)", tinCl1);
        Assert.Contains("Chốt lời 1: chạm mục tiêu <code>12</code>", tinCl1);
        Assert.Contains("Cắt còn lại: <code>10.5</code>", tinCl1);

        // Cùng format: số dòng bằng nhau, câu chặn bán nửa lần hai giống nhau từng ký tự.
        var dongCl1 = tinCl1.Split('\n');
        var dongRut = tinRut.Split('\n');
        Assert.Equal(dongRut.Length, dongCl1.Length);
        Assert.Equal(dongRut[0].Replace("Rút mốc 4%", "Chốt lời 1"), dongCl1[0]);
        Assert.Equal(dongRut[^2].TrimEnd(), dongCl1[^2].TrimEnd()); // câu "Mỗi chu kỳ tối đa..." (C-16, không in mã)
        Assert.StartsWith("Cắt còn lại:", dongCl1[^3].Trim());
        Assert.StartsWith("Cắt còn lại:", dongRut[^3].Trim());
        Assert.Contains("Mỗi chu kỳ tối đa một lần bán nửa", tinCl1);
        Assert.DoesNotContain("(C-", tinCl1);
    }

    [Fact]
    public void C30_MoiLoaiTinDen_KhongChuaMaQuyetDinhNoiBo()
    {
        // Duyệt mọi loại tin V2 bằng dữ liệu giả — không tin nào được chứa "(C-" (C-30).
        var cacTin = new[]
        {
            V2TelegramFormatter.FormatMua(ViThiMuaCoDiem2()),
            V2TelegramFormatter.FormatMua(ViThiMua(coDuoi: true, doiDungLo: false)),
            V2TelegramFormatter.FormatMuaDiem2("HPG", "NỔ HƯỚNG LÊN", 10.5m, 11.5m, 11m, 12m, 9.5m),
            V2TelegramFormatter.FormatBanNua("HPG", "Chốt lời 1", 12.05m, 10.5m, "Chốt lời 1: chạm mục tiêu <code>12</code>"),
            V2TelegramFormatter.FormatBanNua("HPG", "Rút mốc 4%", 11.5m, 9.5m, "Rút mốc 4%: mốc <code>12</code> giảm ≥ 4%"),
            V2TelegramFormatter.FormatBanHetRutMoc("HPG", 11.2m, 10.5m, 12m),
            V2TelegramFormatter.FormatChamMucGia("HPG", "Chạm dừng lỗ", "BÁN HẾT", 9.4m, 10.5m, null),
            V2TelegramFormatter.FormatCanhBaoKichBanThoat("HPG", "KIỆT SỨC", 10.4m, 9.5m),
            V2TelegramFormatter.FormatCanhBaoChuaBanDuoc("HPG", "CHẠM DỪNG LỖ", 2),
            V2TelegramFormatter.FormatHetThoiGian("HPG", 10.4m, 10.5m, 5, 1m, 3m),
            V2TelegramFormatter.FormatHetHanTheoDoi("HPG", 10.4m, 10.5m, 21, 20),
        };
        foreach (var tin in cacTin)
            Assert.DoesNotContain("(C-", tin);
    }
}
