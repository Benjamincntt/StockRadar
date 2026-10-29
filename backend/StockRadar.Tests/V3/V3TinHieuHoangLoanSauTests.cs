using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Infrastructure.MarketData;
using StockRadar.Infrastructure.Persistence;

namespace StockRadar.Tests.V3;

/// <summary>
/// Cờ hoảng loạn sâu trên tín hiệu V3 (GET /stocks/{sym}/v3 — BUG-05):
/// lấy từ vị thế gắn qua ViTheV3.TinHieuV3Id; tín hiệu chưa có vị thế → false.
/// </summary>
public sealed class V3TinHieuHoangLoanSauTests
{
    private static ApplicationDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static V3HieuQuaService NewService(ApplicationDbContext db) =>
        new(db, Options.Create(new V3Options()));

    private static TinHieuV3Entity SeedTinHieu(ApplicationDbContext db, long id = 1)
    {
        var t = new TinHieuV3Entity
        {
            Id = id,
            Symbol = "HPG",
            Loai = LoaiKichBanV3.N1,
            NgayTinHieu = new DateOnly(2026, 10, 20),
            GiaTinHieu = 27.3m,
            Hang = 1,
            TrangThai = TrangThaiTinHieuV3.DaVaoLenh,
        };
        db.TinHieuV3.Add(t);
        db.SaveChanges();
        return t;
    }

    private static void SeedViThe(
        ApplicationDbContext db, long tinHieuId, bool hoangLoan = false)
    {
        db.ViTheV3.Add(new ViTheV3Entity
        {
            TinHieuV3Id = tinHieuId,
            Symbol = "HPG",
            Loai = LoaiKichBanV3.N1,
            NgayTinHieu = new DateOnly(2026, 10, 20),
            TrangThai = TrangThaiViTheV3.DangGiu,
            LaHoangLoanSau = hoangLoan,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task TinHieuCoViTheHoangLoanSau_True()
    {
        var db = NewDb();
        var t = SeedTinHieu(db);
        SeedViThe(db, t.Id, hoangLoan: true);

        var result = await NewService(db).LayTinHieuTheoSymbolAsync("HPG");

        Assert.Single(result);
        Assert.True(result[0].LaHoangLoanSau);
    }

    [Fact]
    public async Task TinHieuCoViTheThuong_False()
    {
        var db = NewDb();
        var t = SeedTinHieu(db);
        SeedViThe(db, t.Id, hoangLoan: false);

        var result = await NewService(db).LayTinHieuTheoSymbolAsync("HPG");

        Assert.Single(result);
        Assert.False(result[0].LaHoangLoanSau);
    }

    [Fact]
    public async Task TinHieuChuaCoViThe_False()
    {
        var db = NewDb();
        SeedTinHieu(db);

        var result = await NewService(db).LayTinHieuTheoSymbolAsync("HPG");

        Assert.Single(result);
        Assert.False(result[0].LaHoangLoanSau);
    }
}
