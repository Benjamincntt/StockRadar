using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Entities;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.NhatKy;

/// <summary>
/// US2 — gắn lệnh với tín hiệu V3 (spec 012 FR-040): picker V3 chỉ đọc từ <c>TinHieuV3</c>,
/// chụp giá theo chiều lệnh (mua → giá vào; bán → giá thoát), lệch so tín hiệu,
/// tín hiệu mất vẫn giữ giá chụp. VIP (V1) đã ngừng: lệnh cũ NguonTinHieu="VIP" còn đọc được.
/// </summary>
public sealed class NhatKyTinHieuTests
{
    private static readonly DateTime T0 = new(2026, 10, 20, 2, 0, 0, DateTimeKind.Utc);

    private static ApplicationDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static NhatKyService NewService(ApplicationDbContext db) =>
        new(new EfNhatKyRepository(db), new EfNhatKyTinHieuRepository(db), Options.Create(new RealizedPnlOptions()));

    /// <summary>Seed một tín hiệu V3 đã vào lệnh (có giá vào) — điều kiện duy nhất picker V3 hiển thị.</summary>
    private static TinHieuV3Entity SeedV3(
        ApplicationDbContext db, long id, string symbol = "HPG",
        decimal? giaVao = 27_300m, decimal? giaThoat = null,
        string? ketQua = null, decimal? phanTramLoiNhuan = null,
        LoaiKichBanV3 loai = LoaiKichBanV3.KB1)
    {
        var e = new TinHieuV3Entity
        {
            Id = id,
            Symbol = symbol,
            Loai = loai,
            NgayTinHieu = DateOnly.FromDateTime(T0),
            GiaTinHieu = giaVao ?? 0m,
            Hang = 1,
            GiaTriXepHang = 0m,
            MuaMoCuaPhienSau = true,
            TrangThai = giaVao is not null ? TrangThaiTinHieuV3.DaVaoLenh : TrangThaiTinHieuV3.DaBao,
            GiaVao = giaVao,
            GiaThoat = giaThoat,
            PhanTramLoiNhuan = phanTramLoiNhuan,
            KetQua = ketQua,
            CreatedAt = T0,
            UpdatedAt = T0,
        };
        db.TinHieuV3.Add(e);
        db.SaveChanges();
        return e;
    }

    private static TaoLenhRequest MuaCoTinHieu(string nguon, string ma, decimal gia, decimal kl, DateTime? thoiDiem = null) =>
        new("HPG", "Mua", thoiDiem ?? T0, gia, kl, null, nguon, ma, null, null, null, null);

    // ===== Picker V3 =====

    [Fact]
    public async Task PickerV3_ChiTraTinHieuDaVaoLenh()
    {
        await using var db = NewDb();
        SeedV3(db, 341, giaVao: 27_300m);
        SeedV3(db, 342, symbol: "VNM", giaVao: null); // mới báo, chưa vào lệnh → không hiện
        var svc = NewService(db);

        var ketQua = await svc.LayTinHieuV3Async(Guid.NewGuid(), null, null, 1, 30, default);
        var item = Assert.Single(ketQua.Items);
        Assert.Equal("V3", item.Nguon);
        Assert.Equal("341", item.MaTinHieu);
        Assert.Equal("HPG", item.Symbol);
        Assert.Equal("KB1", item.LoaiTinHieu);
    }

    [Fact]
    public async Task PickerV3_LoaiTinHieuChuaCoGiaVao()
    {
        // 1 tín hiệu đã khớp mua + 1 chỉ mới báo (chưa có giá vào).
        await using var db = NewDb();
        SeedV3(db, 341, symbol: "HPG");
        SeedV3(db, 342, symbol: "VNM", giaVao: null);
        var repo = new EfNhatKyTinHieuRepository(db);

        var (items, total) = await repo.LayTinHieuV3Async(null, null, 1, 30, default);

        Assert.Equal(1, total);
        var item = Assert.Single(items);
        Assert.Equal(341, item.Id);
    }

    [Fact]
    public async Task LayMotTinHieuV3_DocTheoId_CoGiaVao()
    {
        await using var db = NewDb();
        SeedV3(db, 341, giaVao: 27_300m);
        SeedV3(db, 342, giaVao: null);
        var repo = new EfNhatKyTinHieuRepository(db);

        var row = await repo.LayMotTinHieuV3Async(341, default);
        Assert.NotNull(row);
        Assert.Equal(27_300m, row!.GiaVao);

        // Tín hiệu chưa vào lệnh → picker từ chối đọc theo id.
        Assert.Null(await repo.LayMotTinHieuV3Async(342, default));
    }

    [Fact]
    public async Task PickerV3_GiaTinHieu_LayGiaVao()
    {
        await using var db = NewDb();
        SeedV3(db, 341, giaVao: 27_300m);
        var svc = NewService(db);

        var item = Assert.Single((await svc.LayTinHieuV3Async(Guid.NewGuid(), null, null, 1, 30, default)).Items);
        Assert.Equal(27_300m, item.GiaTinHieu);
    }

    [Fact]
    public async Task PickerV3_MirrorsKetQuaTinHieu()
    {
        await using var db = NewDb();
        SeedV3(db, 341, giaVao: 27_000m, giaThoat: 29_000m, ketQua: "Thang", phanTramLoiNhuan: 7.04m);
        var svc = NewService(db);

        var item = Assert.Single((await svc.LayTinHieuV3Async(Guid.NewGuid(), null, null, 1, 30, default)).Items);
        Assert.Equal("Thang", item.KetQuaTinHieu);
    }

    [Fact]
    public async Task PickerV3_DaCoLenhDungCuaUserHienTai()
    {
        await using var db = NewDb();
        SeedV3(db, 341);
        var svc = NewService(db);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await svc.TaoLenhAsync(userA, MuaCoTinHieu("V3", "341", 27_500m, 100), default);

        var cuaA = Assert.Single((await svc.LayTinHieuV3Async(userA, null, null, 1, 30, default)).Items);
        Assert.True(cuaA.DaCoLenh);
        var cuaB = Assert.Single((await svc.LayTinHieuV3Async(userB, null, null, 1, 30, default)).Items);
        Assert.False(cuaB.DaCoLenh);
    }

    // ===== Gắn lệnh V3: chụp giá lúc gắn =====

    [Fact]
    public async Task GanLenhV3_ChupDungGiaTinHieuLucGan()
    {
        await using var db = NewDb();
        SeedV3(db, 341, giaVao: 27_300m);
        var svc = NewService(db);

        var lenh = await svc.TaoLenhAsync(Guid.NewGuid(), MuaCoTinHieu("V3", "341", 27_500m, 100), default);
        Assert.Equal(27_300m, lenh.GiaTinHieuChup);
    }

    [Fact]
    public async Task GanLenh_TinHieuChuaVaoLenh_Bao404()
    {
        // Tín hiệu chưa có giá vào → không gắn được (LayNhomGiaHoac404).
        await using var db = NewDb();
        SeedV3(db, 341, giaVao: null);
        var svc = NewService(db);

        await Assert.ThrowsAsync<NhatKyTinHieuNotFoundException>(() =>
            svc.TaoLenhAsync(Guid.NewGuid(), MuaCoTinHieu("V3", "341", 27_500m, 100), default));
    }

    [Fact]
    public async Task GanLenh_TinHieuKhongTonTai_Bao404()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        await Assert.ThrowsAsync<NhatKyTinHieuNotFoundException>(() =>
            svc.TaoLenhAsync(Guid.NewGuid(), MuaCoTinHieu("V3", "999", 27_500m, 100), default));
    }

    [Fact]
    public async Task LechSoTinHieu_MotLenh_TinhGiaTriVaPhanTram()
    {
        await using var db = NewDb();
        SeedV3(db, 341, giaVao: 27_300m);
        var svc = NewService(db);
        var userId = Guid.NewGuid();

        await svc.TaoLenhAsync(userId, MuaCoTinHieu("V3", "341", 27_500m, 100), default);

        var lenh = Assert.Single((await svc.LayDanhSachAsync(userId, null, null, null, null, 1, 50, default)).Items);
        Assert.Equal(200m, lenh.LechSoTinHieu);
        Assert.Equal(0.73m, lenh.LechSoTinHieuPhanTram);
    }

    [Fact]
    public async Task LechSoTinHieu_HaiLenhBanChiaNho_TheoGiaKhopBinhQuanKL()
    {
        // V3: mua snapshot giá vào; mọi lệnh bán cùng snapshot giá thoát → BQ gộp theo chiều + mức chụp.
        await using var db = NewDb();
        SeedV3(db, 341, giaVao: 27_000m, giaThoat: 30_000m);
        var svc = NewService(db);
        var userId = Guid.NewGuid();

        await svc.TaoLenhAsync(userId, MuaCoTinHieu("V3", "341", 27_000m, 500, thoiDiem: T0.AddDays(-12)), default);
        await svc.TaoLenhAsync(userId, new TaoLenhRequest("HPG", "Ban", T0.AddDays(-11), 30_500m, 250, null, "V3", "341", null, null, null, null), default);
        var s2 = await svc.TaoLenhAsync(userId, new TaoLenhRequest("HPG", "Ban", T0.AddDays(-10), 31_000m, 250, null, "V3", "341", null, null, null, null), default);

        // BQ khớp bán = (250×30.500 + 250×31.000)/500 = 30.750 → lệch = 750 so với giá chụp 30.000.
        var sau = await svc.LayMotAsync(userId, s2.Id, default);
        Assert.Equal(750m, sau!.LechSoTinHieu);
    }

    [Fact]
    public async Task TinHieuBiXoaSauKhiGan_LenhVanGiuGiaChupVaLech()
    {
        await using var db = NewDb();
        SeedV3(db, 341, giaVao: 27_300m);
        var svc = NewService(db);
        var userId = Guid.NewGuid();

        await svc.TaoLenhAsync(userId, MuaCoTinHieu("V3", "341", 27_500m, 100), default);

        var signal = await db.TinHieuV3.SingleAsync(x => x.Id == 341);
        db.TinHieuV3.Remove(signal);
        await db.SaveChangesAsync();

        var lenh = Assert.Single((await svc.LayDanhSachAsync(userId, null, null, null, null, 1, 50, default)).Items);
        Assert.Equal(27_300m, lenh.GiaTinHieuChup);
        Assert.Equal(200m, lenh.LechSoTinHieu);
    }

    [Fact]
    public async Task SuaLenh_CungTinHieu_KhongDoiGiaDaChup()
    {
        await using var db = NewDb();
        SeedV3(db, 341, giaVao: 27_300m);
        var svc = NewService(db);
        var userId = Guid.NewGuid();

        var lenh = await svc.TaoLenhAsync(userId, MuaCoTinHieu("V3", "341", 27_500m, 100), default);

        // Tín hiệu đổi giá nguồn sau khi gắn — sửa lệnh (đổi giá khớp) vẫn giữ giá đã chụp.
        var signal = await db.TinHieuV3.SingleAsync(x => x.Id == 341);
        signal.GiaVao = 99_000m;
        await db.SaveChangesAsync();

        var sua = await svc.SuaLenhAsync(userId, lenh.Id, MuaCoTinHieu("V3", "341", 29_000m, 100), default);
        Assert.Equal(27_300m, sua!.GiaTinHieuChup);
    }

    // ===== Lệnh VIP cũ (Q3 — VIP đã ngừng): còn đọc, không còn picker =====

    [Fact]
    public async Task LenhVipCu_ChupGiaVanConDoc_KetQuaTinHieuDeTrong()
    {
        await using var db = NewDb();
        var idVip = Guid.NewGuid();
        db.NhatKyGiaoDich.Add(new NhatKyGiaoDichEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            MaChungKhoan = "HPG",
            Chieu = "Mua",
            ThoiDiemKhop = T0,
            GiaKhop = 28_200m,
            KhoiLuong = 100m,
            NguonTinHieu = "VIP",
            MaTinHieu = idVip.ToString(),
            GiaTinHieuChup = 28_000m,
            TaoNgay = T0,
        });
        await db.SaveChangesAsync();
        var svc = NewService(db);
        var userId = db.NhatKyGiaoDich.Single().UserId;

        var lenh = Assert.Single((await svc.LayDanhSachAsync(userId, null, null, null, null, 1, 50, default)).Items);
        Assert.Equal(28_000m, lenh.GiaTinHieuChup); // giá đã chụp tại thời điểm gắn (Q3)
        Assert.Null(lenh.KetQuaTinHieu);
        Assert.Null(lenh.KetQuaTinHieuPhanTram);
    }

    [Fact]
    public async Task TaoLenhNguonVip_BiTuongBo_VIPDaNgung()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        await Assert.ThrowsAsync<NhatKyTinHieuNotFoundException>(() =>
            svc.TaoLenhAsync(Guid.NewGuid(), MuaCoTinHieu("VIP", "khong-phai-guid", 28_200m, 100), default));
    }
}
