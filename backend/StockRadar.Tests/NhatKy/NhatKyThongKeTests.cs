using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Entities;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.NhatKy;

/// <summary>
/// US3 (T024/T025) — vị thế mở/đóng + lãi/lỗ ròng theo RealizedPnlMath,
/// thống kê theo kỳ: tỷ lệ thắng (trừ vị thế mở), lệch TB mua/bán tách riêng,
/// tỷ lệ khớp tín hiệu, kết quả tín hiệu kèm lệnh đã đóng, cách ly user.
/// </summary>
public sealed class NhatKyThongKeTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    private static readonly Guid UserA = Guid.NewGuid();

    private static ApplicationDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static NhatKyService NewService(ApplicationDbContext db, RealizedPnlOptions? options = null) =>
        new(new EfNhatKyRepository(db), new EfNhatKyTinHieuRepository(db),
            Options.Create(options ?? new RealizedPnlOptions()));

    private static void SeedV3(
        ApplicationDbContext db, long id, string symbol = "HPG",
        decimal? giaVao = 10_000m, decimal? giaThoat = null,
        string? ketQua = null, decimal? phanTramLoiNhuan = null)
    {
        db.TinHieuV3.Add(new TinHieuV3Entity
        {
            Id = id,
            Symbol = symbol,
            Loai = LoaiKichBanV3.KB1,
            NgayTinHieu = DateOnly.FromDateTime(Now.AddDays(-20)),
            GiaTinHieu = giaVao ?? 0m,
            Hang = 1,
            MuaMoCuaPhienSau = true,
            TrangThai = TrangThaiTinHieuV3.DaVaoLenh,
            GiaVao = giaVao,
            GiaThoat = giaThoat,
            KetQua = ketQua,
            PhanTramLoiNhuan = phanTramLoiNhuan,
            CreatedAt = Now.AddDays(-20),
            UpdatedAt = Now,
        });
        db.SaveChanges();
    }

    private static TaoLenhRequest Lenh(
        string symbol, string chieu, DateTime thoiDiem,
        decimal? gia = null, decimal? kl = null, decimal? phi = null,
        string? nguon = null, string? ma = null, string? ghiChu = null,
        decimal? kLieuChinh = null, decimal? vonDieuChinh = null) =>
        new(symbol, chieu, thoiDiem, gia, kl, phi, nguon, ma, null, ghiChu, kLieuChinh, vonDieuChinh);

    // ===== Vị thế: đóng + lãi/lỗ ròng (RealizedPnlMath) =====

    [Fact]
    public async Task ViThe_DongHon_SauHaiNhipBan_LaiLoRongDungCongThuc()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var mua = Now.AddDays(-12);
        var ban1 = Now.AddDays(-11);
        var ban2 = Now.AddDays(-10);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Mua", mua, 10_000m, 1000m, 15_000m), default);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Ban", ban1, 11_000m, 500m, 19_250m), default);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Ban", ban2, 9_000m, 500m, 15_750m), default);

        var vt = Assert.Single(await svc.LayViTheAsync(UserA, default));
        Assert.True(vt.DaDong);
        Assert.Equal(0m, vt.KhoiLuongMo);
        Assert.Equal(DateOnly.FromDateTime(ban2), vt.NgayDong);
        Assert.Equal(-50_000m, vt.LoiNhuanRongVND);
        Assert.Equal(-0.50m, vt.ReturnOnDeployedPercent);

        // Mốc kiểm: trùng kết quả RealizedPnlMath.Compute — giá vốn 10.015 đã gồm phí mua → chỉ trừ phí bán 0,35%.
        var doiChieu = RealizedPnlMath.Compute(
            10_015m,
            [new SellLeg(DateOnly.FromDateTime(ban1), 11_000m, 500m), new SellLeg(DateOnly.FromDateTime(ban2), 9_000m, 500m)],
            new FeeProfile(0m, 0.35m, 0m));
        Assert.Equal(doiChieu!.ReturnOnDeployedPercent, vt.ReturnOnDeployedPercent);

        Assert.NotNull(vt.NhipBan);
        Assert.Equal(2, vt.NhipBan.Count);
        Assert.Equal(9.45m, vt.NhipBan[0].LaiRongPhanTram);
        Assert.Equal(-10.45m, vt.NhipBan[1].LaiRongPhanTram);
    }

    [Fact]
    public async Task ViThe_MuaNhieuLan_GiaVonBinhQuanTinhCaPhiMua()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Mua", Now.AddDays(-6), 10_000m, 1000m), default);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Mua", Now.AddDays(-5), 12_000m, 1000m), default);

        var vt = Assert.Single(await svc.LayViTheAsync(UserA, default));
        Assert.False(vt.DaDong);
        Assert.Equal(2000m, vt.KhoiLuongMo);
        Assert.Equal(11_016.5m, vt.GiaVonBinhQuan); // phí mua config 0,15%: (10.015+12.018)/2
        Assert.Equal(22_033_000m, vt.TienVonDangKe);
        Assert.Null(vt.LoiNhuanRongVND);
        Assert.Null(vt.ReturnOnDeployedPercent);
        Assert.Empty(vt.NhipBan ?? []);
    }

    [Fact]
    public async Task ViThe_LenhDieuChinh_PhanAnhVaoGiaVonVaKhoiLuong()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Mua", Now.AddDays(-6), 10_000m, 1000m), default);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "DieuChinh", Now.AddDays(-5), ghiChu: "cho them 1000 cp", kLieuChinh: 1000m, vonDieuChinh: 0m), default);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Ban", Now.AddDays(-4), 6_000m, 2000m), default);

        var vt = Assert.Single(await svc.LayViTheAsync(UserA, default));
        Assert.True(vt.DaDong);
        Assert.Equal(19.40m, vt.ReturnOnDeployedPercent); // giaVon 5.007,5; net 5.979 (phí+thuế bán config 0,35%)
        Assert.Equal(1_943_000m, vt.LoiNhuanRongVND);
    }

    [Fact]
    public async Task ViThe_DangMo_CoNhipBanDaThucHienVanQuyDoiLaiRong()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Mua", Now.AddDays(-8), 10_000m, 1000m, 15_000m), default);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Ban", Now.AddDays(-7), 11_000m, 500m, 19_250m), default);

        var vt = Assert.Single(await svc.LayViTheAsync(UserA, default));
        Assert.False(vt.DaDong);
        Assert.Equal(500m, vt.KhoiLuongMo);
        Assert.Equal(10_015m, vt.GiaVonBinhQuan);
        Assert.Equal(473_250m, vt.LoiNhuanRongVND);
        Assert.Equal(9.45m, vt.ReturnOnDeployedPercent);
        Assert.Null(vt.NgayDong);
    }

    // ===== Thống kê theo kỳ =====

    [Fact]
    public async Task ThongKe_TatCa_TyLeThangTruViTheDangMo()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        // HPG đóng thắng; VNM đóng thua; FPT đang mở (không tính vào tỷ lệ thắng).
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Mua", Now.AddDays(-12), 10_000m, 1000m, 15_000m), default);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Ban", Now.AddDays(-11), 11_000m, 1000m, 16_500m), default);
        await svc.TaoLenhAsync(UserA, Lenh("VNM", "Mua", Now.AddDays(-12), 10_000m, 1000m, 15_000m), default);
        await svc.TaoLenhAsync(UserA, Lenh("VNM", "Ban", Now.AddDays(-11), 9_000m, 1000m, 13_500m), default);
        await svc.TaoLenhAsync(UserA, Lenh("FPT", "Mua", Now.AddDays(-3), 25_000m, 100m), default);

        var tk = await svc.LayThongKeAsync(UserA, "all", default);
        Assert.Equal(1, tk.ViThe.Tang);
        Assert.Equal(1, tk.ViThe.Thua);
        Assert.Equal(0, tk.ViThe.Ngang);
        Assert.Equal(1, tk.ViThe.ChoDo);
        Assert.Equal(50.00m, tk.ViThe.TyLeThang);
        Assert.Equal(0m, tk.NguongThangPhanTram); // thắng = lãi ròng > ngưỡng config (0%)
        Assert.Equal(-60_000m, tk.TongLoiNhuanRongVND); // 968.500 − 1.028.500
        Assert.Equal(20_030_000m, tk.TongDoLosuDungVND);
        Assert.Equal(5, tk.SoLenh);
        Assert.Null(tk.TyLeKhopTinHieu); // chưa gắn/bỏ qua tín hiệu nào
        Assert.Null(tk.LechTBSoTinHieuPhanTram);
        Assert.Equal(tk.TongLoiNhuanRongVND, tk.TheoKy.Sum(x => x.TongLoiNhuanRongVND));
    }

    [Fact]
    public async Task ThongKe_TheoKy_LocTheoNgayDongCuaNhipBanCuoi()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        // A đóng cách đây 10 ngày (trong tháng), B đóng cách đây 40 ngày (ngoài mốc −30 ngày).
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Mua", Now.AddDays(-12), 10_000m, 1000m, 15_000m), default);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Ban", Now.AddDays(-10), 11_000m, 1000m, 16_500m), default);
        await svc.TaoLenhAsync(UserA, Lenh("VNM", "Mua", Now.AddDays(-42), 10_000m, 1000m, 15_000m), default);
        await svc.TaoLenhAsync(UserA, Lenh("VNM", "Ban", Now.AddDays(-40), 9_000m, 1000m, 13_500m), default);

        var thang = await svc.LayThongKeAsync(UserA, "month", default);
        Assert.Equal(1, thang.ViThe.Tang);
        Assert.Equal(0, thang.ViThe.Thua);
        Assert.Equal(968_500m, thang.TongLoiNhuanRongVND);

        var tatCa = await svc.LayThongKeAsync(UserA, "all", default);
        Assert.Equal(1, tatCa.ViThe.Tang);
        Assert.Equal(1, tatCa.ViThe.Thua);
        Assert.Equal(-60_000m, tatCa.TongLoiNhuanRongVND);
    }

    [Fact]
    public async Task ThongKe_LechTBMuaBanTachRieng()
    {
        await using var db = NewDb();
        SeedV3(db, 701, giaVao: 10_000m, giaThoat: 9_000m);
        var svc = NewService(db);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Mua", Now.AddDays(-3), 10_200m, 1000m, null, "V3", "701"), default);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Ban", Now.AddDays(-2), 8_910m, 1000m, null, "V3", "701"), default);

        var tk = await svc.LayThongKeAsync(UserA, "all", default);
        Assert.Equal(2.00m, tk.LechTBMuaPhanTram);   // (10.200 − 10.000) / 10.000
        Assert.Equal(-1.00m, tk.LechTBBanPhanTram);  // (8.910 − 9.000) / 9.000
        Assert.Equal(0.50m, tk.LechTBSoTinHieuPhanTram);
    }

    [Fact]
    public async Task ThongKe_TyLeKhopTinHieuCoLenhVaBoQua()
    {
        await using var db = NewDb();
        SeedV3(db, 801);
        SeedV3(db, 802);
        SeedV3(db, 803);
        var svc = NewService(db);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Mua", Now.AddDays(-3), 10_000m, 1000m, null, "V3", "801"), default);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Mua", Now.AddDays(-2), 10_500m, 500m, null, "V3", "802"), default);
        await svc.DatBoQuaAsync(UserA, "V3", "803", "nhảy quá xa", default);

        var tk = await svc.LayThongKeAsync(UserA, "all", default);
        Assert.Equal(2, tk.TinHieuCoLenh);
        Assert.Equal(1, tk.TinHieuBoQua);
        Assert.Equal(66.67m, tk.TyLeKhopTinHieu); // 2 / (2 + 1)
    }

    [Fact]
    public async Task LenhGanTinHieuTraKemKetQuaTinHieu_ChiV3_VipCuDeTrong()
    {
        await using var db = NewDb();
        SeedV3(db, 901, ketQua: "Thang", phanTramLoiNhuan: 12.5m);
        var svc = NewService(db);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Mua", Now.AddDays(-3), 10_000m, 1000m, null, "V3", "901"), default);

        // Lệnh VIP cũ (Q3 — VIP đã ngừng): ghi thẳng NguonTinHieu="VIP", không migrate.
        var idVip = Guid.NewGuid();
        db.NhatKyGiaoDich.Add(new NhatKyGiaoDichEntity
        {
            Id = Guid.NewGuid(), UserId = UserA, MaChungKhoan = "HPG", Chieu = "Mua",
            ThoiDiemKhop = Now.AddDays(-2), GiaKhop = 28_200m, KhoiLuong = 100m,
            NguonTinHieu = "VIP", MaTinHieu = idVip.ToString(), GiaTinHieuChup = 28_000m, TaoNgay = Now.AddDays(-2),
        });
        await db.SaveChangesAsync();

        var ds = await svc.LayDanhSachAsync(UserA, null, null, null, null, 1, 50, default);
        var v3 = ds.Items.Single(x => x.MaTinHieu == "901");
        Assert.Equal("Thang", v3.KetQuaTinHieu);
        Assert.Equal(12.5m, v3.KetQuaTinHieuPhanTram);
        var vip = ds.Items.Single(x => x.MaTinHieu == idVip.ToString());
        Assert.Null(vip.KetQuaTinHieu); // nguồn V1 đã gỡ → kết quả tín hiệu để trống
        Assert.Null(vip.KetQuaTinHieuPhanTram);

        var tk = await svc.LayThongKeAsync(UserA, "all", default); // thống kê không lỗi với lệnh VIP cũ
        Assert.Equal(2, tk.SoLenh);
    }

    [Fact]
    public async Task ThongKe_CachLyTheoUser()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Mua", Now.AddDays(-6), 10_000m, 1000m, 15_000m), default);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Ban", Now.AddDays(-5), 11_000m, 1000m, 16_500m), default);

        var userB = Guid.NewGuid();
        Assert.Empty(await svc.LayViTheAsync(userB, default));
        var tk = await svc.LayThongKeAsync(userB, "all", default);
        Assert.Equal(0, tk.ViThe.Tang);
        Assert.Equal(0, tk.ViThe.Thua);
        Assert.Equal(0, tk.ViThe.ChoDo);
        Assert.Null(tk.ViThe.TyLeThang);
        Assert.Equal(0m, tk.TongLoiNhuanRongVND);
        Assert.Equal(0, tk.SoLenh);
        Assert.Null(tk.TyLeKhopTinHieu);
    }

    [Fact]
    public async Task ThongKe_NguongThangHoaVonTinhNgang()
    {
        await using var db = NewDb();
        var svc = NewService(db, new RealizedPnlOptions { BuyFeePercent = 0m, SellFeePercent = 0m, SellTaxPercent = 0m });
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Mua", Now.AddDays(-4), 10_000m, 1000m), default);
        await svc.TaoLenhAsync(UserA, Lenh("HPG", "Ban", Now.AddDays(-3), 11_000m, 1000m), default);
        await svc.TaoLenhAsync(UserA, Lenh("VNM", "Mua", Now.AddDays(-4), 10_000m, 1000m), default);
        await svc.TaoLenhAsync(UserA, Lenh("VNM", "Ban", Now.AddDays(-3), 10_000m, 1000m), default);
        await svc.TaoLenhAsync(UserA, Lenh("FPT", "Mua", Now.AddDays(-4), 10_000m, 1000m), default);
        await svc.TaoLenhAsync(UserA, Lenh("FPT", "Ban", Now.AddDays(-3), 9_000m, 1000m), default);

        var tk = await svc.LayThongKeAsync(UserA, "all", default);
        Assert.Equal(1, tk.ViThe.Tang);
        Assert.Equal(1, tk.ViThe.Ngang); // hoà vốn = Flat theo Classify với ngưỡng 0
        Assert.Equal(1, tk.ViThe.Thua);
        Assert.Equal(33.33m, tk.ViThe.TyLeThang);
    }
}
