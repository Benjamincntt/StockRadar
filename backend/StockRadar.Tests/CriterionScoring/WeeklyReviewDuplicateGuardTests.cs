using Microsoft.EntityFrameworkCore;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Repositories;
using Xunit;

namespace StockRadar.Tests.CriterionScoring;

/// <summary>
/// Bảng weekly khoá theo (WeekStartDate, CriterionId) và (WeekStartDate, GroupId).
/// Ghi hai dòng cùng khoá làm hỏng DbContext của cả scope — xem docs/domain/pipeline-jobs.md.
/// </summary>
public sealed class WeeklyReviewDuplicateGuardTests
{
    private static readonly DateOnly WeekStart = new(2026, 9, 14);

    private static ApplicationDbContext NewDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static WeeklyCriterionReviewSnapshot Criterion(CriterionType type, string groupId) =>
        new(type, groupId, type.ToString(), 1, 5, 10, 50m, 60m, 1m, CriterionReviewAction.Keep, true);

    private static CriterionGroupWeeklySnapshot Group(string groupId, int hit = 5, int total = 10) =>
        new(groupId, hit, total, 50m, 60m, 1, 0, 0, CriterionReviewAction.Keep);

    [Fact]
    public async Task UpsertWeeklyReviews_TrungGroupId_ThiBaoLoiNgay()
    {
        await using var db = NewDb();
        var repo = new EfCriterionScoringRepository(db);

        var groups = new[] { Group("Momentum"), Group("Momentum") };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            repo.UpsertWeeklyReviewsAsync(WeekStart, [], groups, DateTime.UtcNow));

        Assert.Contains("Momentum", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpsertWeeklyReviews_TrungCriterionId_ThiBaoLoiNgay()
    {
        await using var db = NewDb();
        var repo = new EfCriterionScoringRepository(db);

        var criteria = new[]
        {
            Criterion(CriterionType.Volume, "Khối lượng"),
            Criterion(CriterionType.Volume, "Khối lượng"),
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repo.UpsertWeeklyReviewsAsync(WeekStart, criteria, [], DateTime.UtcNow));
    }

    [Fact]
    public async Task UpsertWeeklyReviews_KhongTrung_ThiGhiDuocVaKhongHongDbContext()
    {
        await using var db = NewDb();
        var repo = new EfCriterionScoringRepository(db);

        var criteria = new[] { Criterion(CriterionType.Volume, "Khối lượng") };
        var groups = new[] { Group("Khối lượng"), Group("Momentum") };

        await repo.UpsertWeeklyReviewsAsync(WeekStart, criteria, groups, DateTime.UtcNow);

        Assert.Equal(1, await db.WeeklyCriterionReviews.CountAsync());
        Assert.Equal(2, await db.CriterionGroupWeeklyReviews.CountAsync());

        // Ghi tiếp trên cùng DbContext phải chạy được — đây là bước đã chết khi entry bị detach.
        await repo.UpsertWeeklyReviewsAsync(WeekStart, criteria, groups, DateTime.UtcNow);
        Assert.Equal(2, await db.CriterionGroupWeeklyReviews.CountAsync());
    }
}
