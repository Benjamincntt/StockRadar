using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.NhatKy;

/// <summary>
/// US2 — đánh dấu tín hiệu V3 bỏ qua / không khớp (Q3, FR-040): upsert idempotent,
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

    private static void SeedV3(ApplicationDbContext db, long id, string symbol = "HPG")
    {
        db.TinHieuV3.Add(new TinHieuV3Entity
        {
            Id = id,
            Symbol = symbol,
            Loai = LoaiKichBanV3.KB1,
            NgayTinHieu = DateOnly.FromDateTime(T0),
            GiaTinHieu = 27_300m,
            Hang = 1,
            MuaMoCuaPhienSau = true,
            TrangThai = TrangThaiTinHieuV3.DaVaoLenh,
            GiaVao = 27_300m,
            CreatedAt = T0,
            UpdatedAt = T0,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task DatBoQua_UpsertIdempotent_MotDongDuocLyDoMoiNhat()
    {
        await using var db = NewDb();
        SeedV3(db, 341);
        var svc = NewService(db);
        var userId = Guid.NewGuid();

        await svc.DatBoQuaAsync(userId, "v3", "341", "nhảy quá xa", default);
        await svc.DatBoQuaAsync(userId, "V3", "341", "đổi ý: vượt nền mạnh", default);

        var dong = await db.NhatKyTinHieuBoQua.Where(x => x.UserId == userId).ToListAsync();
        Assert.Single(dong);
        Assert.Equal("đổi ý: vượt nền mạnh", dong[0].LyDo);
        Assert.Equal("V3", dong[0].Nguon);
    }

    [Fact]
    public async Task DatBoQua_TinHieuKhongTonTai_Bao404()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        await Assert.ThrowsAsync<NhatKyTinHieuNotFoundException>(() =>
            svc.DatBoQuaAsync(Guid.NewGuid(), "v3", "999", null, default));
    }

    [Fact]
    public async Task XoaBoQua_KhongCoDanhDau_KhongBan()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        await svc.XoaBoQuaAsync(Guid.NewGuid(), "v3", "341", default);
    }

    [Fact]
    public async Task GhiLenhGanTinHieu_TuXoaDanhDauBoQua()
    {
        await using var db = NewDb();
        SeedV3(db, 341);
        var svc = NewService(db);
        var userId = Guid.NewGuid();

        await svc.DatBoQuaAsync(userId, "V3", "341", "để ý trước", default);
        await svc.TaoLenhAsync(userId,
            new TaoLenhRequest("HPG", "Mua", T0, 27_500m, 100, null, "V3", "341", null, null, null, null), default);

        Assert.Null(await db.NhatKyTinHieuBoQua.FirstOrDefaultAsync(
            x => x.UserId == userId && x.Nguon == "V3" && x.MaTinHieu == "341"));
    }

    [Fact]
    public async Task BoQua_CachLyUserB_KhongThayKhongXoaDuocCuaA()
    {
        await using var db = NewDb();
        SeedV3(db, 341);
        var svc = NewService(db);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await svc.DatBoQuaAsync(userA, "V3", "341", "A bỏ qua", default);

        // Picker của B không thấy đánh dấu của A.
        var itemB = Assert.Single((await svc.LayTinHieuV3Async(userB, null, null, 1, 30, default)).Items);
        Assert.False(itemB.DaBoQua);
        var itemA = Assert.Single((await svc.LayTinHieuV3Async(userA, null, null, 1, 30, default)).Items);
        Assert.True(itemA.DaBoQua);

        // B bỏ đánh dấu (không có gì thuộc B) → đánh dấu của A vẫn còn.
        await svc.XoaBoQuaAsync(userB, "V3", "341", default);
        Assert.NotNull(await db.NhatKyTinHieuBoQua.FirstOrDefaultAsync(
            x => x.UserId == userA && x.MaTinHieu == "341"));
    }
}
