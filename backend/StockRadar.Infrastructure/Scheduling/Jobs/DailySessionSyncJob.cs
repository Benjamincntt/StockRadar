using Microsoft.Extensions.Logging;
using Quartz;
using StockRadar.Application.Abstractions;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Scheduling.Jobs;

/// <summary>Job 2 — đồng bộ OHLCV phiên sau 15h VN.</summary>
[DisallowConcurrentExecution]
internal sealed class DailySessionSyncJob(
    IDailySessionSyncService sync,
    ILogger<DailySessionSyncJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!VietnamMarketCalendar.IsTradingDay(VietnamMarketCalendar.TodayVietnam()))
        {
            logger.LogDebug("Bỏ qua Job 2 — không phải ngày giao dịch.");
            return;
        }

        logger.LogInformation("Quartz — Job 2: đồng bộ phiên hôm nay.");
        await sync.RunAsync(context.CancellationToken);
    }
}
