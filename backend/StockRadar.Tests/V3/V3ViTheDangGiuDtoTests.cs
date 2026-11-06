using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Infrastructure.MarketData;
using StockRadar.Infrastructure.Persistence;

namespace StockRadar.Tests.V3;

/// <summary>
/// Spec 016 D9 — DTO vị thế đang giữ phải mang mã tín hiệu và khối lượng để app mở Nhật ký điền sẵn.
/// Dữ liệu bịa, DB trong bộ nhớ.
/// </summary>
public sealed class V3ViTheDangGiuDtoTests
{
    private static readonly DateTime T0 = new(2026, 11, 5, 9, 0, 0, DateTimeKind.Utc);

    private static ApplicationDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task ViTheDangGiu_MangMaTinHieuVaKhoiLuong()
    {
        await using var db = NewDb();
        db.ViTheV3.Add(new ViTheV3Entity
        {
            Id = 11,
            TinHieuV3Id = 901,
            Symbol = "AAA",
            Loai = LoaiKichBanV3.KB1,
            NgayTinHieu = new DateOnly(2026, 11, 3),
            TrangThai = TrangThaiViTheV3.DangGiu,
            GiaVao = 54m,
            KhoiLuong = 200m,
            SoPhienDaGi = 3,
            CreatedAt = T0,
            UpdatedAt = T0,
        });
        await db.SaveChangesAsync();

        var ds = await new V3HieuQuaService(db, Options.Create(new V3Options())).LayViTheDangGiuAsync();

        var dto = Assert.Single(ds);
        Assert.Equal(901, dto.TinHieuV3Id);
        Assert.Equal(200m, dto.KhoiLuong);
    }
}
