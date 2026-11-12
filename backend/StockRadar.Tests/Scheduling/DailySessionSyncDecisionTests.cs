using StockRadar.Infrastructure.Scheduling.Jobs;
using static StockRadar.Infrastructure.Scheduling.Jobs.DailySessionSyncDecision;

namespace StockRadar.Tests.Scheduling;

/// <summary>
/// Spec 018 Q2/Q4 — Job 2 chỉ còn các trigger sau đóng cửa (15:05, 15:15, 15:30),
/// nên quyết định chỉ còn hai nhánh: ngày nghỉ thì bỏ qua, còn lại chọn lần chính hay thử lại.
/// Nhịp V3 16:00 phụ thuộc nến ATC do lần 15:05 ghi — kiểm ở DailySessionSyncRetryTests.
/// </summary>
public sealed class DailySessionSyncDecisionTests
{
    [Fact]
    public void LanChinh_KhongCoCoThuLai_ChayLanChinh()
    {
        var result = Decide(isTradingDay: true, isRetryTrigger: false);

        Assert.Equal(Result.RunLanChinh, result);
    }

    [Fact]
    public void CoThuLai_ChayLanThuLai()
    {
        var result = Decide(isTradingDay: true, isRetryTrigger: true);

        Assert.Equal(Result.RunThuLai, result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NgayNghi_LuonBoQua(bool isRetryTrigger)
    {
        var result = Decide(isTradingDay: false, isRetryTrigger: isRetryTrigger);

        Assert.Equal(Result.SkipNotTradingDay, result);
    }
}
