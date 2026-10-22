using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.NhatKy;

/// <summary>
/// N6/FR-009 — cách ly theo UserId: user B không thấy/sửa/xoá được lệnh của user A;
/// service map về null/false → controller trả 404 (không lộ sự tồn tại).
/// </summary>
public sealed class NhatKyCachLyTests
{
    private static readonly DateTime T0 = new(2026, 10, 20, 2, 0, 0, DateTimeKind.Utc);

    private static ApplicationDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static NhatKyService NewService(ApplicationDbContext db)
    {
        INhatKyRepository repo = new EfNhatKyRepository(db);
        return new NhatKyService(repo, new EfNhatKyTinHieuRepository(db), Options.Create(new RealizedPnlOptions()));
    }

    private static TaoLenhRequest Mua(string symbol = "HPG") =>
        new(symbol, "Mua", T0, 10_000m, 100, null, null, null, null, null, null, null);

    [Fact]
    public async Task UserB_KhongListDuocLenhCuaUserA()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await svc.TaoLenhAsync(userA, Mua(), default);

        var cuaB = await svc.LayDanhSachAsync(userB, null, null, null, null, 1, 50, default);
        Assert.Empty(cuaB.Items);
        Assert.Equal(0, cuaB.TotalCount);

        var cuaA = await svc.LayDanhSachAsync(userA, null, null, null, null, 1, 50, default);
        Assert.Equal(1, cuaA.TotalCount);
    }

    [Fact]
    public async Task UserB_KhongLayDuocDonLenhCuaUserA()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var lenhA = await svc.TaoLenhAsync(userA, Mua(), default);

        Assert.Null(await svc.LayMotAsync(userB, lenhA.Id, default));
        Assert.NotNull(await svc.LayMotAsync(userA, lenhA.Id, default));
    }

    [Fact]
    public async Task UserB_SuaLenhUserA_TrảNullVaDuLieuKhongDoi()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var lenhA = await svc.TaoLenhAsync(userA, Mua(), default);

        Assert.Null(await svc.SuaLenhAsync(userB, lenhA.Id, Mua(), default));

        var vanA = await svc.LayMotAsync(userA, lenhA.Id, default);
        Assert.Equal(10_000m, vanA!.GiaKhop);
    }

    [Fact]
    public async Task UserB_XoaLenhUserA_TrảFalseVaLenhVanCon()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var lenhA = await svc.TaoLenhAsync(userA, Mua(), default);

        Assert.False(await svc.XoaLenhAsync(userB, lenhA.Id, default));
        Assert.Equal(1, await db.NhatKyGiaoDich.CountAsync(x => x.Id == lenhA.Id));
    }

    [Fact]
    public async Task UserB_KhongThayViTheCuaUserA()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await svc.TaoLenhAsync(userA, Mua(), default);

        Assert.Empty(await svc.LayViTheAsync(userB, default));
        Assert.Single(await svc.LayViTheAsync(userA, default));
    }

    [Fact]
    public async Task LenhLuonGanVoiUserIdTuToken_KhongPhaiTuBody()
    {
        await using var db = NewDb();
        var svc = NewService(db);
        var userA = Guid.NewGuid();

        // TaoLenhRequest không có trường userId — dòng lưu bắt buộc mang userId của token.
        var lenh = await svc.TaoLenhAsync(userA, Mua(), default);
        var row = await db.NhatKyGiaoDich.SingleAsync(x => x.Id == lenh.Id);
        Assert.Equal(userA, row.UserId);
    }
}
