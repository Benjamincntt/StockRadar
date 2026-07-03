using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Scheduling.Jobs;

/// <summary>Job 2 — đồng bộ OHLCV phiên (lặp trong giờ GD hoặc cron sau 15h VN).</summary>
[DisallowConcurrentExecution]
internal sealed class DailySessionSyncJob(
    IDailySessionSyncService sync,
    IOptions<MarketJobsOptions> options,
    ILogger<DailySessionSyncJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!VietnamMarketCalendar.IsTradingDay(VietnamMarketCalendar.TodayVietnam()))
        {
            logger.LogDebug("Bỏ qua Job 2 — không phải ngày giao dịch.");
            return;
        }

        var cfg = options.Value.DailySession;
        if (cfg.IntervalMinutes > 0
            && !cfg.ForceRunOutsideHours
            && !VietnamMarketCalendar.IsMarketOpen())
        {
            logger.LogDebug("Bỏ qua Job 2 — ngoài giờ giao dịch (interval {Min} phút).", cfg.IntervalMinutes);
            return;
        }

        logger.LogInformation("Quartz — Job 2: đồng bộ phiên hôm nay.");
        await sync.RunAsync(context.CancellationToken);
    }
}
