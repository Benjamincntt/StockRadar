using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Services;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Entities;
using StockRadar.Infrastructure.Persistence.Repositories;
using OutcomeBucketNames = StockRadar.Domain.MasterAlerts.OutcomeBucketNames;

namespace StockRadar.Tests.RealizedPnl;

/// <summary>
/// Chốt rủi ro R3 của plan: aggregate realized PHẢI tính từ <c>MasterAlertPositions</c> (1 dòng = 1 lệnh),
/// KHÔNG từ SetupTracks — 1 vị thế có cả track MuaDiem1 + MuaDiem2 không được đếm 2 lần.
/// </summary>
public sealed class RealizedAggregateTests
{
    private static ApplicationDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Position_with_both_buy1_and_buy2_tracks_counts_once_in_alert_history_aggregate()
    {
        using var db = NewDb();

        var position = new MasterAlertPositionEntity
        {
            Id = Guid.NewGuid(),
            Symbol = "DUP1",
            EntryDate = new DateOnly(2026, 7, 1),
            EntryPrice = 100m,
            PeakPriceSinceEntry = 110m,
            CurrentPositionSize = 0m,
            MaxPositionSize = 1.0m,
            FiredAlertKindsJson = "[]",
            IsClosed = true,
            ClosedDate = new DateOnly(2026, 7, 10),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RealizedMeasured = true,
            RealizedMeasuredAt = DateTime.UtcNow,
            RealizedStatus = RealizedStatusNames.Measured,
            RealizedOutcomeBucket = OutcomeBucketNames.Good,
            RealizedReturnOnDeployedPercent = 10m,
            RealizedWeightedReturnPercent = 10m,
            RealizedGrossReturnPercent = 10m,
            HoldingSessions = 5,
        };
        db.MasterAlertPositions.Add(position);

        // 1 vị thế nhưng 2 track (MuaDiem1 ngày mua ban đầu, MuaDiem2 khi nâng size) — cùng PositionId.
        db.SetupTracks.Add(new SetupTrackEntity
        {
            Id = Guid.NewGuid(),
            Symbol = "DUP1",
            SourceType = MasterAlertKinds.BuyPoint1,
            EntryDate = new DateOnly(2026, 7, 1),
            EntryPrice = 100m,
            PositionId = position.Id,
            OutcomeMeasured = true,
            OutcomeBucket = "Good",
        });
        db.SetupTracks.Add(new SetupTrackEntity
        {
            Id = Guid.NewGuid(),
            Symbol = "DUP1",
            SourceType = MasterAlertKinds.BuyPoint2,
            EntryDate = new DateOnly(2026, 7, 2),
            EntryPrice = 102m,
            PositionId = position.Id,
            OutcomeMeasured = true,
            OutcomeBucket = "Good",
        });
        db.SaveChanges();

        var repo = new EfSetupTrackRepository(db);
        var page = await repo.GetAlertHistoryAsync(
            limit: 50,
            skip: 0,
            outcomeMeasured: null,
            sourceType: null,
            buyPointsOnly: true);

        // 2 track trong page (list hiển thị vẫn 2 dòng)…
        Assert.Equal(2, page.Alerts.Count);
        // …nhưng aggregate realized (từ MasterAlertPositions) chỉ đếm 1 lệnh, không phải 2.
        Assert.Equal(1, page.TotalClosedTrades);
        Assert.Equal(0, page.TotalOpenTrades);
        Assert.Equal(1, page.RealizedWinCount);
        Assert.Equal(0, page.RealizedLoseCount);
        Assert.Equal(100m, page.RealizedWinRatePercent);
        Assert.Equal(10m, page.AvgRealizedReturnPercent);

        // Mỗi track hiển thị realized field join từ vị thế (PositionIsClosed qua RealizedStatus/HoldingSessions).
        Assert.All(page.Alerts, a => Assert.Equal(OutcomeBucketNames.Good, a.RealizedOutcomeBucket));
        Assert.All(page.Alerts, a => Assert.Equal(5, a.HoldingSessions));
    }

    [Fact]
    public void Trend_builder_dedups_realized_count_by_position_id_within_bucket()
    {
        var positionId = Guid.NewGuid();
        var entryDate = new DateOnly(2026, 7, 1); // cùng tuần → cùng bucket

        SetupTrackRecord MakeTrack(string sourceType, DateOnly trackEntryDate, decimal entryPrice) => new(
            Id: Guid.NewGuid(),
            Symbol: "DUP2",
            SourceType: sourceType,
            EntryDate: trackEntryDate,
            EntryPrice: entryPrice,
            OpportunityForDate: null,
            OpportunityRank: null,
            OpportunityScore: null,
            SessionChangePercent: null,
            SessionVolume: null,
            PeakGainPercent: null,
            OutcomeMeasured: true,
            ForwardPriceT25: 101m,
            ForwardReturnPercent: 1m,
            OutcomeBucket: "Good",
            MeasuredAt: DateTime.UtcNow,
            WeekStartDate: null,
            PositionId: positionId,
            PositionIsClosed: true,
            RealizedReturnPercent: 12m,
            RealizedOutcomeBucket: OutcomeBucketNames.Good,
            RealizedStatus: RealizedStatusNames.Measured);

        var tracks = new List<SetupTrackRecord>
        {
            MakeTrack(MasterAlertKinds.BuyPoint1, entryDate, 100m),
            MakeTrack(MasterAlertKinds.BuyPoint2, entryDate.AddDays(1), 102m),
        };

        var result = AlertHistoryTrendBuilder.Build("week", tracks, limit: 12, selectedPeriodStart: null);

        var bucket = Assert.Single(result.Buckets);
        Assert.Equal(1, bucket.RealizedClosedCount);
        Assert.Equal(1, bucket.RealizedWinCount);
        Assert.Equal(100m, bucket.RealizedWinRatePercent);
        Assert.Equal(12m, bucket.AvgRealizedReturnPercent);
    }
}
