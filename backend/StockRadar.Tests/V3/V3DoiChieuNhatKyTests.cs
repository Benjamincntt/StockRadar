using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;

namespace StockRadar.Tests.V3;

/// <summary>
/// SC-002 (spec 014) — luật đối chiếu vị thế V3 với lệnh thật trong Nhật ký giao dịch (plan D3).
/// Toàn bộ số liệu là dữ liệu BỊA để kiểm luật, không lấy từ tài khoản thật.
/// </summary>
public sealed class V3DoiChieuNhatKyTests
{
    private static readonly V3CauHinh Cfg = new();
    private static readonly DateOnly HomNay = new(2026, 11, 2);
    private const long TinHieuId = 901;

    private static V3DoiChieuNhatKy.ViThe ViTheDangGiu(
        decimal khoiLuong = 200m, decimal giaVao = 53.2m, bool hoangLoanSau = false) =>
        new(TinHieuId, "HPG", TrangThaiViTheV3.DangGiu, HomNay, giaVao, khoiLuong, null, hoangLoanSau);

    private static V3DoiChieuNhatKy.ViThe ViTheDaThoat(decimal giaVao = 53.2m, decimal khoiLuong = 200m) =>
        new(TinHieuId, "HPG", TrangThaiViTheV3.DaThoat, HomNay.AddDays(-3), giaVao, khoiLuong, HomNay, false);

    private static V3DoiChieuNhatKy.Lenh Mua(
        decimal khoiLuong, decimal gia, DateOnly? ngay = null, string symbol = "HPG",
        string? nguon = V3DoiChieuNhatKy.NguonV3, string? maTinHieu = "901") =>
        new(symbol, "Mua", ngay ?? HomNay, gia, khoiLuong, nguon, maTinHieu);

    private static V3DoiChieuNhatKy.Lenh Ban(
        decimal khoiLuong, decimal gia, DateOnly? ngay = null, string symbol = "HPG") =>
        new(symbol, "Ban", ngay ?? HomNay, gia, khoiLuong, V3DoiChieuNhatKy.NguonV3, "901");

    private static V3DoiChieuNhatKy.KetQua DoiChieu(
        V3DoiChieuNhatKy.ViThe vt, IReadOnlyList<V3DoiChieuNhatKy.Lenh> cacLenh, bool biBoQua = false) =>
        V3DoiChieuNhatKy.DoiChieu(vt, HomNay, cacLenh, biBoQua, Cfg);

    [Fact]
    public void Khong_nhat_ky_thi_giu_nguyen()
    {
        var kq = DoiChieu(ViTheDangGiu(), Array.Empty<V3DoiChieuNhatKy.Lenh>());

        Assert.False(kq.CoThayDoi);
        Assert.Same(V3DoiChieuNhatKy.KetQua.KhongDoi, kq);
    }

    [Fact]
    public void Mot_lenh_mua_ghi_de_khoiLuong_giaVao_giaTriLenh()
    {
        // 200 cp × 53,20 (hệ thống) → chủ nhân mua thật 100 cp × 53,40.
        var kq = DoiChieu(ViTheDangGiu(), [Mua(100m, 53.4m)]);

        Assert.NotNull(kq.Mua);
        Assert.Equal(100m, kq.Mua!.KhoiLuong);
        Assert.Equal(53.4m, kq.Mua.GiaVao);
        Assert.Equal(5340m, kq.Mua.GiaTriLenh);
        // Lệnh thường: ngưỡng bán để bước (2) của runner tính theo giá vào mới.
        Assert.Null(kq.Mua.NguongChamMaHoangLoan);
    }

    [Fact]
    public void Hai_lenh_mua_tinh_gia_vao_binh_quan_gia_quyen()
    {
        // 60 × 53,00 + 40 × 54,00 = 5.340 / 100 cp = 53,40.
        var kq = DoiChieu(ViTheDangGiu(), [Mua(60m, 53.0m), Mua(40m, 54.0m)]);

        Assert.Equal(100m, kq.Mua!.KhoiLuong);
        Assert.Equal(53.4m, kq.Mua.GiaVao);
        Assert.Equal(5340m, kq.Mua.GiaTriLenh);
    }

    [Fact]
    public void Hoang_loan_sau_dat_nguong_theo_gia_vao_moi()
    {
        // FR-007: bước (2) có DO-NOT-CHANGE nên bước đối chiếu phải tự đặt ngưỡng = giá vào × 1,15.
        var kq = DoiChieu(ViTheDangGiu(hoangLoanSau: true), [Mua(100m, 53.4m)]);

        Assert.Equal(61.41m, kq.Mua!.NguongChamMaHoangLoan);
    }

    [Fact]
    public void Bo_qua_tin_hieu_then_huy_vi_the()
    {
        var kq = DoiChieu(ViTheDangGiu(), [Mua(100m, 53.4m)], biBoQua: true);

        Assert.True(kq.BiHuy);
        Assert.Null(kq.Mua);
        Assert.Null(kq.Ban);
    }

    [Fact]
    public void Bo_qua_khi_nhat_ky_chua_co_lenh_nao_van_huy_vi_the()
    {
        var kq = DoiChieu(ViTheDangGiu(), [], biBoQua: true);

        Assert.True(kq.BiHuy);
        Assert.True(kq.CoThayDoi);
    }

    [Fact]
    public void Ban_het_khi_vi_the_da_thoat_tinh_lai_va_ket_qua_moi()
    {
        // Hệ thống thoát 53,20 → 54,00; chủ nhân bán thật 200 cp × 54,50.
        var kq = DoiChieu(ViTheDaThoat(), [Ban(200m, 54.5m)]);

        Assert.NotNull(kq.Ban);
        Assert.Equal(54.5m, kq.Ban!.GiaThoat);
        // 54,5/53,2 − 1 − 0,4% phí = 2,0436% → làm tròn 4 số.
        Assert.Equal(2.0436m, kq.Ban.PhanTramLoiNhuan);
        Assert.Equal("Thang", kq.Ban.XepLoai);
        Assert.False(kq.Ban.DongViThe); // đã DaThoat — chỉ ghi lại số, giữ LyDoThoat của hệ thống
    }

    [Fact]
    public void Ban_het_khi_vi_the_con_giu_dong_lenh_theo_nhat_ky()
    {
        var kq = DoiChieu(ViTheDangGiu(), [Ban(200m, 53.0m)]);

        Assert.NotNull(kq.Ban);
        Assert.Equal(53.0m, kq.Ban!.GiaThoat);
        Assert.Equal(-0.7759m, kq.Ban.PhanTramLoiNhuan);
        Assert.Equal("Thua", kq.Ban.XepLoai);
        Assert.True(kq.Ban.DongViThe); // runner gọi KhopBanAsync với lý do BanTheoNhatKy
        Assert.Equal(HomNay, kq.Ban.NgayThoat);
    }

    [Fact]
    public void Lenh_khop_ngay_khac_thi_khong_tinh()
    {
        // C2: hạn nhập là nhịp 16:00 của chính phiên khớp — lệnh khớp hôm qua không còn được đọc.
        var kq = DoiChieu(ViTheDangGiu(), [Mua(100m, 53.4m, ngay: HomNay.AddDays(-1))]);

        Assert.False(kq.CoThayDoi);
    }

    [Fact]
    public void Lenh_gan_tin_hieu_khac_hoac_khong_gan_thi_khong_tinh()
    {
        var ganTinHieuKhac = DoiChieu(ViTheDangGiu(), [Mua(100m, 53.4m, maTinHieu: "902")]);
        var khongGanTinHieu = DoiChieu(ViTheDangGiu(), [Mua(100m, 53.4m, nguon: null, maTinHieu: null)]);
        var ganNguonKhac = DoiChieu(ViTheDangGiu(), [Mua(100m, 53.4m, nguon: "VIP")]);

        Assert.False(ganTinHieuKhac.CoThayDoi);
        Assert.False(khongGanTinHieu.CoThayDoi);
        Assert.False(ganNguonKhac.CoThayDoi);
    }

    [Fact]
    public void Ban_mot_phan_thi_giu_so_he_thong_va_canh_bao()
    {
        // P2: bản đầu chỉ áp khi bán hết — bán 100/200 cp thì giữ số hệ thống, trả lý do để runner log.
        var kq = DoiChieu(ViTheDangGiu(), [Ban(100m, 54.0m)]);

        Assert.True(kq.BanMotPhan);
        Assert.Null(kq.Ban);
        Assert.False(kq.CoThayDoi);
    }

    [Fact]
    public void Lenh_lech_ma_ck_thi_bo_qua_va_dem_canh_bao()
    {
        // Rủi ro "nhập nhầm mã tín hiệu": đúng tín hiệu nhưng mã CK là DXG → không được ghi đè HPG.
        var kq = DoiChieu(ViTheDangGiu(), [Mua(100m, 53.4m, symbol: "DXG")]);

        Assert.Equal(1, kq.SoLenhLechMa);
        Assert.Null(kq.Mua);
        Assert.False(kq.CoThayDoi);
    }
}
