using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StockRadar.Domain.Entities;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Entities;
using StockRadar.Infrastructure.Persistence.Mapping;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.MuaTet;

/// <summary>
/// Giá tham chiếu cho số trong ngày của giỏ Mùa Tết: đóng cửa nến liền trước nến cuối đã lưu, kèm ngày nến cuối.
/// Nến bịa, không phải dữ liệu thật.
/// </summary>
public sealed class GiaThamChieuRepositoryTests
{
    private static async Task<EfStockRepository> TaoRepoAsync(params (string Symbol, OhlcvBar[] Nen)[] cacMa)
    {
        var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        foreach (var (symbol, nen) in cacMa)
        {
            db.Stocks.Add(new StockEntity
            {
                Symbol = symbol,
                Name = symbol,
                HistoryJson = JsonSerializer.Serialize(nen, EntityMapper.JsonOptions),
                LastClose = nen.Length > 0 ? nen[^1].Close : 0m
            });
        }

        await db.SaveChangesAsync();
        return new EfStockRepository(db);
    }

    private static OhlcvBar Nen(int ngay, decimal close) =>
        new(new DateOnly(2026, 11, ngay), close, close, close, close, 1_000);

    [Fact]
    public async Task Lay_dong_cua_nen_lien_truoc_nen_cuoi_va_ngay_nen_cuoi()
    {
        var repo = await TaoRepoAsync(("AAA", [Nen(18, 9.8m), Nen(19, 10m), Nen(20, 10.4m)]));

        var kq = await repo.LayGiaThamChieuAsync(["aaa "]);

        var aaa = Assert.Single(kq).Value;
        Assert.Equal(new DateOnly(2026, 11, 20), aaa.NgayPhien);
        Assert.Equal(10m, aaa.GiaThamChieu);
    }

    [Fact]
    public async Task Ma_duoi_hai_nen_hoac_khong_co_trong_db_thi_vang_mat()
    {
        var repo = await TaoRepoAsync(("AAA", [Nen(20, 10m)]), ("BBB", [Nen(19, 5m), Nen(20, 5.1m)]));

        var kq = await repo.LayGiaThamChieuAsync(["AAA", "BBB", "CCC"]);

        Assert.Equal(["BBB"], kq.Keys);
    }
}
