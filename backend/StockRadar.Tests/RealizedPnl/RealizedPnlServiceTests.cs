using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Infrastructure.Persistence;
using OutcomeBucketNames = StockRadar.Domain.MasterAlerts.OutcomeBucketNames;
using StockRadar.Infrastructure.Persistence.Entities;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Tests.RealizedPnl;

/// <summary>
/// <see cref="RealizedPnlService.MeasureClosedPositionsAsync"/> — idempotency, auto-recompute khi đổi phí,
/// MissingSellPrice khi không có leg, bỏ qua vị thế còn mở, Approximate khi leg là giá backfill.
/// </summary>
public sealed class RealizedPnlServiceTests
{
    private static ApplicationDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static RealizedPnlService NewService(
        ApplicationDbContext db,
        decimal buyFee = 0m,
        decimal sellFee = 0m,
        decimal sellTax = 0m,
        decimal winThreshold = 0m)
    {
        var repo = new EfMasterAlertPositionRepository(db);
        var options = Options.Create(new RealizedPnlOptions
        {
            Enabled = true,
            BuyFeePercent = buyFee,
            SellFeePercent = sellFee,
            SellTaxPercent = sellTax,
            WinThresholdPercent = winThreshold,
            MeasureLookbackSessions = 500,
        });
        return new RealizedPnlService(repo, options, NullLogger<RealizedPnlService>.Instance);
    }

    private static MasterAlertPositionEntity SeedClosedPosition(
        ApplicationDbContext db,
        decimal entryPrice = 100m,
        decimal maxPositionSize = 1.0m,
        DateOnly? entryDate = null,
        DateOnly? closedDate = null)
    {
        var entity = new MasterAlertPositionEntity
        {
            Id = Guid.NewGuid(),
            Symbol = "TEST",
            EntryDate = entryDate ?? new DateOnly(2026, 7, 1),
            EntryPrice = entryPrice,
            PeakPriceSinceEntry = entryPrice,
            CurrentPositionSize = 0m,
            MaxPositionSize = maxPositionSize,
            FiredAlertKindsJson = "[]",
            IsClosed = true,
            ClosedDate = closedDate ?? new DateOnly(2026, 7, 10),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.MasterAlertPositions.Add(entity);
        db.SaveChanges();
        return entity;
    }

    private static void SeedLeg(
        ApplicationDbContext db,
        Guid positionId,
        string signal,
        decimal sellPrice,
        decimal soldSize,
        string priceSource = "Fire",
        DateOnly? sellDate = null)
    {
        db.PositionSellLegs.Add(new PositionSellLegEntity
        {
            Id = Guid.NewGuid(),
            PositionId = positionId,
            Symbol = "TEST",
            Signal = signal,
            SellDate = sellDate ?? new DateOnly(2026, 7, 8),
            SellPrice = sellPrice,
            SoldSize = soldSize,
            RemainingSizeAfter = 0m,
            PriceSource = priceSource,
            FiredAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Measures_closed_position_and_is_idempotent_on_second_run()
    {
        using var db = NewDb();
        var position = SeedClosedPosition(db, entryPrice: 100m, maxPositionSize: 1.0m);
        SeedLeg(db, position.Id, MasterAlertKinds.SellPoint1Half, 110m, 0.5m);
        SeedLeg(db, position.Id, MasterAlertKinds.SellAll, 105m, 0.5m);

        var service = NewService(db);

        var firstRun = await service.MeasureClosedPositionsAsync();
        Assert.Equal(1, firstRun);

        var updated = await db.MasterAlertPositions.AsNoTracking().FirstAsync(x => x.Id == position.Id);
        Assert.True(updated.RealizedMeasured);
        Assert.Equal(RealizedStatusNames.Measured, updated.RealizedStatus);
        Assert.Equal(7.5m, updated.RealizedReturnOnDeployedPercent);
        Assert.Equal(OutcomeBucketNames.Good, updated.RealizedOutcomeBucket);
        Assert.NotNull(updated.RealizedMeasuredAt);
        Assert.NotNull(updated.HoldingSessions);

        // Lần 2: RealizedFeeProfile đã khớp feeKey hiện tại → GetClosedPendingRealizedAsync trả rỗng.
        var secondRun = await service.MeasureClosedPositionsAsync();
        Assert.Equal(0, secondRun);
    }

    [Fact]
    public async Task Changing_fee_profile_triggers_recompute()
    {
        using var db = NewDb();
        var position = SeedClosedPosition(db, entryPrice: 100m, maxPositionSize: 1.0m);
        SeedLeg(db, position.Id, MasterAlertKinds.SellAll, 110m, 1.0m);

        var serviceOldFees = NewService(db, buyFee: 0m, sellFee: 0m, sellTax: 0m);
        Assert.Equal(1, await serviceOldFees.MeasureClosedPositionsAsync());

        var afterFirst = await db.MasterAlertPositions.AsNoTracking().FirstAsync(x => x.Id == position.Id);
        Assert.Equal(10m, afterFirst.RealizedReturnOnDeployedPercent);

        // Đổi phí → feeKey khác → auto-recompute dù RealizedMeasured đã true.
        var serviceNewFees = NewService(db, buyFee: 0.15m, sellFee: 0.25m, sellTax: 0.1m);
        Assert.Equal(1, await serviceNewFees.MeasureClosedPositionsAsync());

        var afterSecond = await db.MasterAlertPositions.AsNoTracking().FirstAsync(x => x.Id == position.Id);
        Assert.True(afterSecond.RealizedReturnOnDeployedPercent < afterFirst.RealizedReturnOnDeployedPercent);
    }

    [Fact]
    public async Task Closed_position_without_legs_is_marked_missing_sell_price()
    {
        using var db = NewDb();
        var position = SeedClosedPosition(db);
        var service = NewService(db);

        var measured = await service.MeasureClosedPositionsAsync();

        Assert.Equal(1, measured);
        var updated = await db.MasterAlertPositions.AsNoTracking().FirstAsync(x => x.Id == position.Id);
        Assert.True(updated.RealizedMeasured);
        Assert.Equal(RealizedStatusNames.MissingSellPrice, updated.RealizedStatus);
        Assert.Null(updated.RealizedReturnOnDeployedPercent);
        Assert.Null(updated.RealizedOutcomeBucket);
    }

    [Fact]
    public async Task Open_position_is_not_measured()
    {
        using var db = NewDb();
        var entity = new MasterAlertPositionEntity
        {
            Id = Guid.NewGuid(),
            Symbol = "OPEN1",
            EntryDate = new DateOnly(2026, 7, 1),
            EntryPrice = 100m,
            PeakPriceSinceEntry = 100m,
            CurrentPositionSize = 1.0m,
            MaxPositionSize = 1.0m,
            FiredAlertKindsJson = "[]",
            IsClosed = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.MasterAlertPositions.Add(entity);
        db.SaveChanges();

        var service = NewService(db);
        var measured = await service.MeasureClosedPositionsAsync();

        Assert.Equal(0, measured);
        var stillOpen = await db.MasterAlertPositions.AsNoTracking().FirstAsync(x => x.Id == entity.Id);
        Assert.False(stillOpen.RealizedMeasured);
    }

    [Fact]
    public async Task Leg_from_forward_t25_backfill_marks_status_approximate()
    {
        using var db = NewDb();
        var position = SeedClosedPosition(db, entryPrice: 100m, maxPositionSize: 1.0m);
        SeedLeg(db, position.Id, MasterAlertKinds.SellAll, 108m, 1.0m, priceSource: "ForwardT25");

        var service = NewService(db);
        Assert.Equal(1, await service.MeasureClosedPositionsAsync());

        var updated = await db.MasterAlertPositions.AsNoTracking().FirstAsync(x => x.Id == position.Id);
        Assert.Equal(RealizedStatusNames.Approximate, updated.RealizedStatus);
        Assert.Equal(8m, updated.RealizedReturnOnDeployedPercent);
    }
}
