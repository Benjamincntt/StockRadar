using Microsoft.EntityFrameworkCore;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Entities;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.SellExit;

/// <summary>
/// Regression cho bug hardcode <c>soldSize = 0.5m</c> ở nhánh SellPoint1Half (đã sửa trong
/// <see cref="EfMasterAlertPositionRepository.RecordSellHalfAsync"/>): size hiện tại phải được halving thật
/// (<c>CurrentPositionSize / 2</c>), không phải luôn trừ đúng 0.5.
/// </summary>
public sealed class SellHalfSizeTests
{
    private static ApplicationDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MasterAlertPositionEntity SeedPosition(ApplicationDbContext db, decimal currentSize)
    {
        var entity = new MasterAlertPositionEntity
        {
            Id = Guid.NewGuid(),
            Symbol = "TEST",
            EntryDate = new DateOnly(2026, 7, 1),
            EntryPrice = 100m,
            PeakPriceSinceEntry = 100m,
            CurrentPositionSize = currentSize,
            MaxPositionSize = currentSize,
            FiredAlertKindsJson = "[]",
            IsClosed = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.MasterAlertPositions.Add(entity);
        db.SaveChanges();
        return entity;
    }

    [Fact]
    public async Task Size_1_records_leg_half_and_remaining_half()
    {
        using var db = NewDb();
        var repo = new EfMasterAlertPositionRepository(db);
        var position = SeedPosition(db, 1.0m);

        await repo.RecordSellHalfAsync(position.Id, new DateOnly(2026, 7, 8), 105m, DateTime.UtcNow, "Fire");

        var leg = Assert.Single(db.PositionSellLegs.Where(x => x.PositionId == position.Id));
        Assert.Equal(MasterAlertKinds.SellPoint1Half, leg.Signal);
        Assert.Equal(0.5m, leg.SoldSize);
        Assert.Equal(0.5m, leg.RemainingSizeAfter);
        Assert.Equal("Fire", leg.PriceSource);

        var updated = await db.MasterAlertPositions.FirstAsync(x => x.Id == position.Id);
        Assert.Equal(0.5m, updated.CurrentPositionSize);
        Assert.Contains(MasterAlertKinds.SellPoint1Half, DeserializeKinds(updated.FiredAlertKindsJson));
    }

    [Fact]
    public async Task Size_half_records_leg_quarter_and_remaining_quarter_regression_for_hardcoded_half()
    {
        // Bug cũ: hardcode soldSize=0.5m bất kể size hiện tại → lệnh size 0.5 bị "bán nửa" thành bán hết luôn.
        using var db = NewDb();
        var repo = new EfMasterAlertPositionRepository(db);
        var position = SeedPosition(db, 0.5m);

        await repo.RecordSellHalfAsync(position.Id, new DateOnly(2026, 7, 8), 105m, DateTime.UtcNow, "Fire");

        var leg = Assert.Single(db.PositionSellLegs.Where(x => x.PositionId == position.Id));
        Assert.Equal(0.25m, leg.SoldSize);
        Assert.Equal(0.25m, leg.RemainingSizeAfter);

        var updated = await db.MasterAlertPositions.FirstAsync(x => x.Id == position.Id);
        Assert.Equal(0.25m, updated.CurrentPositionSize);
    }

    [Fact]
    public async Task Zero_size_guard_does_not_throw_and_does_not_insert_leg()
    {
        // Hot path Telegram VIP: soldSize <= 0 → chỉ append kind, KHÔNG insert leg, KHÔNG throw.
        using var db = NewDb();
        var repo = new EfMasterAlertPositionRepository(db);
        var position = SeedPosition(db, 0m);

        var ex = await Record.ExceptionAsync(() =>
            repo.RecordSellHalfAsync(position.Id, new DateOnly(2026, 7, 8), 105m, DateTime.UtcNow, "Fire"));

        Assert.Null(ex);
        Assert.Empty(db.PositionSellLegs.Where(x => x.PositionId == position.Id));

        var updated = await db.MasterAlertPositions.FirstAsync(x => x.Id == position.Id);
        Assert.Equal(0m, updated.CurrentPositionSize);
        Assert.Contains(MasterAlertKinds.SellPoint1Half, DeserializeKinds(updated.FiredAlertKindsJson));
    }

    [Fact]
    public async Task Retry_after_leg_already_recorded_is_idempotent_and_does_not_double_halve()
    {
        // Unique index (PositionId, Signal) + check-then-insert: gọi lại không tạo leg thứ 2, không trừ size lần nữa.
        using var db = NewDb();
        var repo = new EfMasterAlertPositionRepository(db);
        var position = SeedPosition(db, 1.0m);

        await repo.RecordSellHalfAsync(position.Id, new DateOnly(2026, 7, 8), 105m, DateTime.UtcNow, "Fire");
        await repo.RecordSellHalfAsync(position.Id, new DateOnly(2026, 7, 8), 105m, DateTime.UtcNow, "Fire");

        Assert.Single(db.PositionSellLegs.Where(x => x.PositionId == position.Id));
        var updated = await db.MasterAlertPositions.FirstAsync(x => x.Id == position.Id);
        Assert.Equal(0.5m, updated.CurrentPositionSize);
    }

    private static List<string> DeserializeKinds(string json) =>
        System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
}
