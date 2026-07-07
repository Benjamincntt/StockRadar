using Microsoft.Extensions.Logging;
using Quartz;
using StockRadar.Application.Abstractions;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Scheduling.Jobs;

/// <summary>Phân tích SmartMoney + chấm điểm tiêu chí sau Job 2.</summary>
[DisallowConcurrentExecution]
internal sealed class DailyAnalysisJob(
    IDailySessionSyncService session,
    IDailyAnalysisService analysis,
    ILogger<DailyAnalysisJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!VietnamMarketCalendar.IsTradingDay(VietnamMarketCalendar.TodayVietnam()))
        {
            logger.LogDebug("Bỏ qua phân tích — không phải ngày giao dịch.");
            return;
        }

        var slot = context.Trigger.Key.Name.Contains("morning", StringComparison.OrdinalIgnoreCase)
            ? "hết phiên sáng"
            : "sau đóng cửa";
        logger.LogInformation("Quartz — Job 2 (đảm bảo) + phân tích SmartMoney ({Slot}).", slot);
        await session.RunAsync(context.CancellationToken);
        await analysis.RunAsync(context.CancellationToken);
    }
}
