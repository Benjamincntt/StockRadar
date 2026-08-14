using Microsoft.Extensions.Logging;
using Quartz;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;

namespace StockRadar.Infrastructure.Scheduling.Jobs;

/// <summary>
/// Job 1 — backfill lịch sử OHLCV (chạy khi khởi động, thủ công qua API, hoặc định kỳ hàng
/// tuần chế độ đêm — xem <see cref="Options.HistoryJobOptions.WeeklyRefreshEnabled"/>).
/// </summary>
[DisallowConcurrentExecution]
internal sealed class HistoryBackfillJob(
    IHistoryBackfillService backfill,
    ILogger<HistoryBackfillJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var mode = context.MergedJobDataMap.GetString("Mode");
        mode = string.IsNullOrWhiteSpace(mode) ? "fast" : mode;
        logger.LogInformation("Quartz — Job 1: backfill lịch sử ({Mode}).", mode);
        await backfill.RunAsync(new HistoryBackfillRequest(Mode: mode), context.CancellationToken);
    }
}
