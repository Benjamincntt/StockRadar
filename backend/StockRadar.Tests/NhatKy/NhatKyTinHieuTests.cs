using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Entities;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.NhatKy;

/// <summary>
/// US2 — gắn lệnh với tín hiệu (T019 + T021): picker V2 chỉ đọc, chụp giá lúc gắn,
/// lệch so tín hiệu (1 lệnh và nhiều lệnh chia nhỏ theo BQ KL), tín hiệu mất vẫn giữ giá chụp.
/// VIP (V1) đã ngừng: hết picker, lệnh cũ NguonTinHieu="VIP" còn đọc được, kết quả để trống.
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

    private static string KeHoachJson(decimal min, decimal max = 0m) =>
        JsonSerializer.Serialize(new KeHoachGiaoDich
        {
            GiaVaoLenhMin = min,
            GiaVaoLenhMax = max == 0m ? min : max,
            GiaDungLo = min * 0.95m,
            GiaChotLoi1 = min * 1.10m,
            GiaChotLoi2 = min * 1.20m,
            DieuKienHuy = "test",
        });

    private static KetQuaKichBanEntity SeedV2(
        ApplicationDbContext db, long id, string symbol = "HPG",
        TrangThaiKichBan trangThai = TrangThaiKichBan.DaKichHoat,
        decimal? giaVaoThucTe = 27_300m, decimal keHoachMin = 27_100m,
        decimal? giaMuaDiem2 = null, decimal? giaBanNua = null, decimal? giaThoatHet = null,
        string? ketQuaOverride = null)
    {
        var e = new KetQuaKichBanEntity
        {
            Id = id,
            Symbol = symbol,
            LoaiKichBan = LoaiKichBan.NoHuongLen,
            TrangThai = trangThai,
            DatBoiCanh = true,
            DatHinhThai = true,
            DatCoKichHoat = true,
            MucHoanThien = 100m,
            NgayDanhGia = T0,
            ThoiGianKichHoat = trangThai >= TrangThaiKichBan.DaKichHoat ? T0 : null,
            GiaVaoThucTe = giaVaoThucTe,
            GiaMuaDiem2ThucTe = giaMuaDiem2,
            GiaBanNua = giaBanNua,
            GiaThoatHet = giaThoatHet,
            KeHoachGiaoDichJson = KeHoachJson(keHoachMin),
            KetQuaDoLuong = ketQuaOverride ?? (trangThai == TrangThaiKichBan.ChotLoi ? "Thang" : null),
        };
        db.KetQuaKichBan.Add(e);
        db.SaveChanges();
        return e;
    }

    private static TaoLenhRequest MuaCoTinHieu(string nguon, string ma, decimal gia, decimal kl, DateTime? thoiDiem = null) =>
        new("HPG", "Mua", thoiDiem ?? T0, gia, kl, null, nguon, ma, null, null, null, null);

    // ===== Picker V2 =====

    [Fact]
    public async Task PickerV2_ChiTraKichBanDaKichHoat()
    {
        await using var db = NewDb();
        SeedV2(db, 341, trangThai: TrangThaiKichBan.DaKichHoat);
        SeedV2(db, 342, symbol: "VNM", trangThai: TrangThaiKichBan.DangHinhThanh);
        var svc = NewService(db);

        var ketQua = await svc.LayTinHieuV2Async(Guid.NewGuid(), null, null, 1, 30, default);
        var item = Assert.Single(ketQua.Items);
        Assert.Equal("V2", item.Nguon);
        Assert.Equal("341", item.MaTinHieu);
        Assert.Equal("HPG", item.Symbol);
    }

    [Fact]
    public async Task PickerV2_LoaiTinHieuDaBiHuy()
    {
        // Arrange: 1 tín hiệu thật + 1 đã kích hoạt nhưng bị huỷ (KetQuaDoLuong="Huy")
        await using var db = NewDb();
        SeedV2(db, 341, symbol: "HPG");
        SeedV2(db, 342, symbol: "VNM", ketQuaOverride: "Huy");
        var repo = new EfNhatKyTinHieuRepository(db);

        // Act
        var (items, total) = await repo.LayTinHieuV2Async(null, null, 1, 30, default);

        // Assert: picker chỉ còn tín hiệu chưa huỷ
        Assert.Equal(1, total);
        var item = Assert.Single(items);
        Assert.Equal(341, item.Id);
    }

    [Fact]
    public async Task LayMotTinHieuV2_TinHieuDaHuy_VanDocTheoId()
    {
        // Lệnh nhật ký đã gắn tín hiệu bị huỷ trước đó vẫn đọc được theo Id (không đụng dữ liệu cũ).
        await using var db = NewDb();
        SeedV2(db, 341, giaVaoThucTe: 27_300m, ketQuaOverride: "Huy");
        var repo = new EfNhatKyTinHieuRepository(db);

        var row = await repo.LayMotTinHieuV2Async(341, default);

        Assert.NotNull(row);
        Assert.Equal(27_300m, row!.GiaVaoThucTe);
    }

    [Fact]
    public async Task PickerV2_GiaTinHieu_LayGiaVaoThucTe()
    {
        await using var db = NewDb();
        SeedV2(db, 341, giaVaoThucTe: 27_300m, keHoachMin: 27_100m);
        var svc = NewService(db);

        var item = Assert.Single((await svc.LayTinHieuV2Async(Guid.NewGuid(), null, null, 1, 30, default)).Items);
        Assert.Equal(27_300m, item.GiaTinHieu);
        Assert.NotNull(item.KeHoach);
        Assert.Equal(27_100m, item.KeHoach!.GiaVaoMin);
    }

    [Fact]
    public async Task PickerV2_ThieuGiaVaoThucTe_DungBienDuoiVaoLenh()
    {
        await using var db = NewDb();
        SeedV2(db, 341, giaVaoThucTe: null, keHoachMin: 27_100m);
        var svc = NewService(db);

        var item = Assert.Single((await svc.LayTinHieuV2Async(Guid.NewGuid(), null, null, 1, 30, default)).Items);
        Assert.Equal(27_100m, item.GiaTinHieu);
    }

    [Fact]
    public async Task PickerV2_DaCoLenhDungCuaUserHienTai()
    {
        await using var db = NewDb();
        SeedV2(db, 341);
        var svc = NewService(db);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await svc.TaoLenhAsync(userA, MuaCoTinHieu("V2", "341", 27_500m, 100), default);

        var cuaA = Assert.Single((await svc.LayTinHieuV2Async(userA, null, null, 1, 30, default)).Items);
        Assert.True(cuaA.DaCoLenh);
        var cuaB = Assert.Single((await svc.LayTinHieuV2Async(userB, null, null, 1, 30, default)).Items);
        Assert.False(cuaB.DaCoLenh);
    }

    // ===== Gắn lệnh V2: chụp giá lúc gắn =====

    [Fact]
    public async Task GanLenhV2_ChupDungGiaTinHieuLucGan()
    {
        await using var db = NewDb();
        SeedV2(db, 341, giaVaoThucTe: 27_300m);
        var svc = NewService(db);

        var lenh = await svc.TaoLenhAsync(Guid.NewGuid(), MuaCoTinHieu("V2", "341", 27_500m, 100), default);
        Assert.Equal(27_300m, lenh.GiaTinHieuChup);
    }

    [Fact]
    public async Task GanLenhV2_ThieuGiaThucTe_ChupBienDuoiKeHoach()
    {
        await using var db = NewDb();
        SeedV2(db, 341, giaVaoThucTe: null, keHoachMin: 27_100m);
        var svc = NewService(db);

        var lenh = await svc.TaoLenhAsync(Guid.NewGuid(), MuaCoTinHieu("V2", "341", 27_500m, 100), default);
        Assert.Equal(27_100m, lenh.GiaTinHieuChup);
    }

    [Fact]
    public async Task GanLenh_TinHieuKhongTonTai_Bao404()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        await Assert.ThrowsAsync<NhatKyTinHieuNotFoundException>(() =>
            svc.TaoLenhAsync(Guid.NewGuid(), MuaCoTinHieu("V2", "999", 27_500m, 100), default));
    }

    [Fact]
    public async Task LechSoTinHieu_MotLenh_TinhGiaTriVaPhanTram()
    {
        await using var db = NewDb();
        SeedV2(db, 341, giaVaoThucTe: 27_300m);
        var svc = NewService(db);
        var userId = Guid.NewGuid();

        await svc.TaoLenhAsync(userId, MuaCoTinHieu("V2", "341", 27_500m, 100), default);

        var lenh = Assert.Single((await svc.LayDanhSachAsync(userId, null, null, null, null, 1, 50, default)).Items);
        Assert.Equal(200m, lenh.LechSoTinHieu);
        Assert.Equal(0.73m, lenh.LechSoTinHieuPhanTram);
    }

    [Fact]
    public async Task LechSoTinHieu_HaiLenhChiaNho_TheoGiaKhopBinhQuanKL()
    {
        await using var db = NewDb();
        SeedV2(db, 341, giaVaoThucTe: 27_000m, giaBanNua: 29_000m, giaThoatHet: 30_000m);
        var svc = NewService(db);
        var userId = Guid.NewGuid();

        await svc.TaoLenhAsync(userId, MuaCoTinHieu("V2", "341", 27_000m, 1_000, thoiDiem: T0.AddDays(-12)), default);
        // N8: hai lệnh bán sau cùng bậc "bán hết" → BQ chỉ gộp lệnh cùng chiều + cùng mức chụp.
        await svc.TaoLenhAsync(userId, new TaoLenhRequest("HPG", "Ban", T0.AddDays(-11), 28_900m, 500, null, "V2", "341", null, null, null, null), default);
        await svc.TaoLenhAsync(userId, new TaoLenhRequest("HPG", "Ban", T0.AddDays(-10), 30_500m, 250, null, "V2", "341", null, null, null, null), default);
        var s3 = await svc.TaoLenhAsync(userId, new TaoLenhRequest("HPG", "Ban", T0.AddDays(-9), 31_000m, 250, null, "V2", "341", null, null, null, null), default);

        // BQ khớp = (250×30.500 + 250×31.000)/500 = 30.750 → lệch = 750 so với giá chụp 30.000.
        var sau = await svc.LayMotAsync(userId, s3.Id, default);
        Assert.Equal(750m, sau!.LechSoTinHieu);
    }

    [Fact]
    public async Task TinHieuBiXoaSauKhiGan_LenhVanGiuGiaChupVaLech()
    {
        await using var db = NewDb();
        SeedV2(db, 341, giaVaoThucTe: 27_300m);
        var svc = NewService(db);
        var userId = Guid.NewGuid();

        await svc.TaoLenhAsync(userId, MuaCoTinHieu("V2", "341", 27_500m, 100), default);

        var signal = await db.KetQuaKichBan.SingleAsync(x => x.Id == 341);
        db.KetQuaKichBan.Remove(signal);
        await db.SaveChangesAsync();

        var lenh = Assert.Single((await svc.LayDanhSachAsync(userId, null, null, null, null, 1, 50, default)).Items);
        Assert.Equal(27_300m, lenh.GiaTinHieuChup);
        Assert.Equal(200m, lenh.LechSoTinHieu);
    }

    [Fact]
    public async Task SuaLenh_CungTinHieu_KhongDoiGiaDaChup()
    {
        await using var db = NewDb();
        SeedV2(db, 341, giaVaoThucTe: 27_300m);
        var svc = NewService(db);
        var userId = Guid.NewGuid();

        var lenh = await svc.TaoLenhAsync(userId, MuaCoTinHieu("V2", "341", 27_500m, 100), default);

        // Tín hiệu đổi giá nguồn sau khi gắn — sửa lệnh (đổi giá khớp) vẫn giữ giá đã chụp.
        var signal = await db.KetQuaKichBan.SingleAsync(x => x.Id == 341);
        signal.GiaVaoThucTe = 99_000m;
        await db.SaveChangesAsync();

        var sua = await svc.SuaLenhAsync(userId, lenh.Id, MuaCoTinHieu("V2", "341", 29_000m, 100), default);
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
