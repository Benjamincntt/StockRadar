using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Scheduling.Jobs;

/// <summary>Phân tích SmartMoney + chấm điểm tiêu chí sau Job 2 (full);
/// refresh Top selection-only trong phiên (intraday).</summary>
[DisallowConcurrentExecution]
internal sealed class DailyAnalysisJob(
    IDailySessionSyncService session,
    IDailyAnalysisService analysis,
    IOptions<MarketJobsOptions> marketJobs,
    ILogger<DailyAnalysisJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!VietnamMarketCalendar.IsTradingDay(VietnamMarketCalendar.TodayVietnam()))
        {
            logger.LogDebug("Bỏ qua phân tích — không phải ngày giao dịch.");
            return;
        }

        var triggerName = context.Trigger.Key.Name;
        var isIntraday = triggerName.Contains("intraday", StringComparison.OrdinalIgnoreCase);

        if (isIntraday)
        {
            if (!IsInIntradayWindow(marketJobs.Value.DailyAnalysis, out var reason))
            {
                logger.LogDebug("Bỏ qua refresh Top intraday — {Reason}.", reason);
                return;
            }

            logger.LogInformation("Quartz — refresh Top intraday (selection-only, không Job 2).");
            await analysis.RunAsync(
                context.CancellationToken,
                runPostProcessing: false,
                includeStructureAndTracking: false);
            return;
        }

        var slot = triggerName.Contains("morning", StringComparison.OrdinalIgnoreCase)
            ? "hết phiên sáng"
            : "sau đóng cửa";
        logger.LogInformation("Quartz — Job 2 (đảm bảo) + phân tích SmartMoney ({Slot}).", slot);
        await session.RunAsync(context.CancellationToken);
        await analysis.RunAsync(context.CancellationToken);
    }

    /// <summary>Chỉ chạy trong khung phiên: sáng 9:00–11:30 và chiều 13:00–14:45 (mặc định).</summary>
    internal static bool IsInIntradayWindow(DailyAnalysisJobOptions cfg, out string skipReason)
    {
        var now = VietnamMarketCalendar.NowVietnam().TimeOfDay;
        var morningStart = new TimeSpan(cfg.IntradayMorningStartHour, cfg.IntradayMorningStartMinute, 0);
        var morningEnd = new TimeSpan(cfg.IntradayMorningEndHour, cfg.IntradayMorningEndMinute, 0);
        var afternoonStart = new TimeSpan(cfg.IntradayAfternoonStartHour, cfg.IntradayAfternoonStartMinute, 0);
        var afternoonEnd = new TimeSpan(cfg.IntradayAfternoonEndHour, cfg.IntradayAfternoonEndMinute, 0);

        if (now >= morningStart && now <= morningEnd)
        {
            skipReason = "";
            return true;
        }

        if (now >= afternoonStart && now <= afternoonEnd)
        {
            skipReason = "";
            return true;
        }

        skipReason = $"ngoài khung {morningStart:hh\\:mm}–{morningEnd:hh\\:mm} / {afternoonStart:hh\\:mm}–{afternoonEnd:hh\\:mm} (hiện {now:hh\\:mm})";
        return false;
    }
}
