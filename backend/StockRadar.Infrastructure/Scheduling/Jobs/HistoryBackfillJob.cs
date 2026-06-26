using Microsoft.Extensions.Logging;
using Quartz;
using StockRadar.Application.Abstractions;

namespace StockRadar.Infrastructure.Scheduling.Jobs;

/// <summary>Job 1 — backfill lịch sử OHLCV (chạy một lần khi khởi động hoặc thủ công qua API).</summary>
[DisallowConcurrentExecution]
internal sealed class HistoryBackfillJob(
    IHistoryBackfillService backfill,
    ILogger<HistoryBackfillJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Quartz — Job 1: backfill lịch sử.");
        await backfill.RunAsync(cancellationToken: context.CancellationToken);
    }
}
