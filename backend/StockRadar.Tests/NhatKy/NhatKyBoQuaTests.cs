using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.NhatKy;

/// <summary>
/// US2 — đánh dấu tín hiệu bỏ qua / không khớp (Q3, FR-003): upsert idempotent,
/// tự xoá khi ghi lệnh gắn tín hiệu, cách ly theo người dùng.
/// </summary>
public sealed class NhatKyBoQuaTests
{
    private static readonly DateTime T0 = new(2026, 10, 20, 2, 0, 0, DateTimeKind.Utc);

    private static ApplicationDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static NhatKyService NewService(ApplicationDbContext db) =>
        new(new EfNhatKyRepository(db), new EfNhatKyTinHieuRepository(db), Options.Create(new RealizedPnlOptions()));

    private static void SeedV2(ApplicationDbContext db, long id, string symbol = "HPG")
    {
        db.KetQuaKichBan.Add(new KetQuaKichBanEntity
        {
            Id = id,
            Symbol = symbol,
            LoaiKichBan = LoaiKichBan.NoHuongLen,
            TrangThai = TrangThaiKichBan.DaKichHoat,
            DatBoiCanh = true,
            DatHinhThai = true,
            DatCoKichHoat = true,
            MucHoanThien = 100m,
            NgayDanhGia = T0,
            ThoiGianKichHoat = T0,
            GiaVaoThucTe = 27_300m,
            KeHoachGiaoDichJson = JsonSerializer.Serialize(new KeHoachGiaoDich
            {
                GiaVaoLenhMin = 27_100m, GiaVaoLenhMax = 27_100m,
                GiaDungLo = 26_000m, GiaChotLoi1 = 30_000m, GiaChotLoi2 = 32_000m,
                DieuKienHuy = "test",
            }),
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task DatBoQua_UpsertIdempotent_MotDongDuocLyDoMoiNhat()
    {
        await using var db = NewDb();
        SeedV2(db, 341);
        var svc = NewService(db);
        var userId = Guid.NewGuid();

        await svc.DatBoQuaAsync(userId, "v2", "341", "nhảy quá xa", default);
        await svc.DatBoQuaAsync(userId, "V2", "341", "đổi ý: vượt nền mạnh", default);

        var dong = await db.NhatKyTinHieuBoQua.Where(x => x.UserId == userId).ToListAsync();
        Assert.Single(dong);
        Assert.Equal("đổi ý: vượt nền mạnh", dong[0].LyDo);
        Assert.Equal("V2", dong[0].Nguon);
    }

    [Fact]
    public async Task DatBoQua_TinHieuKhongTonTai_Bao404()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        await Assert.ThrowsAsync<NhatKyTinHieuNotFoundException>(() =>
            svc.DatBoQuaAsync(Guid.NewGuid(), "v2", "999", null, default));
    }

    [Fact]
    public async Task XoaBoQua_KhongCoDanhDau_KhongBan()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        await svc.XoaBoQuaAsync(Guid.NewGuid(), "v2", "341", default);
    }

    [Fact]
    public async Task GhiLenhGanTinHieu_TuXoaDanhDauBoQua()
    {
        await using var db = NewDb();
        SeedV2(db, 341);
        var svc = NewService(db);
        var userId = Guid.NewGuid();

        await svc.DatBoQuaAsync(userId, "V2", "341", "để ý trước", default);
        await svc.TaoLenhAsync(userId,
            new TaoLenhRequest("HPG", "Mua", T0, 27_500m, 100, null, "V2", "341", null, null, null, null), default);

        Assert.Null(await db.NhatKyTinHieuBoQua.FirstOrDefaultAsync(
            x => x.UserId == userId && x.Nguon == "V2" && x.MaTinHieu == "341"));
    }

    [Fact]
    public async Task BoQua_CachLyUserB_KhongThayKhongXoaDuocCuaA()
    {
        await using var db = NewDb();
        SeedV2(db, 341);
        var svc = NewService(db);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await svc.DatBoQuaAsync(userA, "V2", "341", "A bỏ qua", default);

        // Picker của B không thấy đánh dấu của A.
        var itemB = Assert.Single((await svc.LayTinHieuV2Async(userB, null, null, 1, 30, default)).Items);
        Assert.False(itemB.DaBoQua);
        var itemA = Assert.Single((await svc.LayTinHieuV2Async(userA, null, null, 1, 30, default)).Items);
        Assert.True(itemA.DaBoQua);

        // B bỏ đánh dấu (không có gì thuộc B) → đánh dấu của A vẫn còn.
        await svc.XoaBoQuaAsync(userB, "V2", "341", default);
        Assert.NotNull(await db.NhatKyTinHieuBoQua.FirstOrDefaultAsync(
            x => x.UserId == userA && x.MaTinHieu == "341"));
    }
}
