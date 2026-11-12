using Microsoft.Extensions.Logging;
using Quartz;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.Enums;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Scheduling.Jobs;

/// <summary>Job 2 — đồng bộ OHLCV phiên sau đóng cửa, kèm hai lần thử lại chỉ lấy mã thiếu (spec 018 Q2/Q4).</summary>
[DisallowConcurrentExecution]
internal sealed class DailySessionSyncJob(
    IDailySessionSyncService sync,
    ILogger<DailySessionSyncJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var isRetryTrigger = context.MergedJobDataMap.ContainsKey(DailySessionSyncDecision.RetryKey);

        var decision = DailySessionSyncDecision.Decide(
            VietnamMarketCalendar.IsTradingDay(VietnamMarketCalendar.TodayVietnam()),
            isRetryTrigger);

        if (decision == DailySessionSyncDecision.Result.SkipNotTradingDay)
        {
            logger.LogDebug("Bỏ qua Job 2 — không phải ngày giao dịch.");
            return;
        }

        var cheDo = decision == DailySessionSyncDecision.Result.RunThuLai
            ? CheDoJob2.ThuLai
            : CheDoJob2.LanChinh;

        logger.LogInformation(
            "Quartz — Job 2: đồng bộ phiên hôm nay (chế độ {CheDo}).",
            cheDo);
        await sync.RunAsync(context.CancellationToken, cheDo);
    }
}
