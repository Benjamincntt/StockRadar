using StockRadar.Infrastructure.Scheduling.Jobs;
using static StockRadar.Infrastructure.Scheduling.Jobs.DailySessionSyncDecision;

namespace StockRadar.Tests.Scheduling;

/// <summary>
/// Spec 016 đợt 1 — quyết định chạy hay bỏ của Job 2, nhất là lần sau đóng cửa phải chạy ngoài giờ
/// để HistoryJson có nến ATC. Chỉ kiểm hàm thuần, không dùng Quartz thật.
/// </summary>
public sealed class DailySessionSyncDecisionTests
{
    [Fact]
    public void AfterCloseTrigger_RunsOutsideMarketHours()
    {
        var result = Decide(isTradingDay: true, isMarketOpen: false, intervalMinutes: 5,
            forceRunOutsideHours: false, isAfterCloseTrigger: true);

        Assert.Equal(Result.Run, result);
    }

    [Fact]
    public void IntervalTrigger_SkipsOutsideMarketHours()
    {
        var result = Decide(isTradingDay: true, isMarketOpen: false, intervalMinutes: 5,
            forceRunOutsideHours: false, isAfterCloseTrigger: false);

        Assert.Equal(Result.SkipOutsideHours, result);
    }

    [Fact]
    public void IntervalTrigger_RunsInsideMarketHours()
    {
        var result = Decide(isTradingDay: true, isMarketOpen: true, intervalMinutes: 5,
            forceRunOutsideHours: false, isAfterCloseTrigger: false);

        Assert.Equal(Result.Run, result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NotTradingDay_AlwaysSkips_EvenForAfterCloseTrigger(bool isAfterCloseTrigger)
    {
        var result = Decide(isTradingDay: false, isMarketOpen: false, intervalMinutes: 5,
            forceRunOutsideHours: true, isAfterCloseTrigger: isAfterCloseTrigger);

        Assert.Equal(Result.SkipNotTradingDay, result);
    }

    [Fact]
    public void ForceRunOutsideHours_KeepsOldMeaning_RunsOutsideHours()
    {
        var result = Decide(isTradingDay: true, isMarketOpen: false, intervalMinutes: 5,
            forceRunOutsideHours: true, isAfterCloseTrigger: false);

        Assert.Equal(Result.Run, result);
    }

    [Fact]
    public void ZeroInterval_RunsOutsideHours_LikeBefore()
    {
        var result = Decide(isTradingDay: true, isMarketOpen: false, intervalMinutes: 0,
            forceRunOutsideHours: false, isAfterCloseTrigger: false);

        Assert.Equal(Result.Run, result);
    }
}
