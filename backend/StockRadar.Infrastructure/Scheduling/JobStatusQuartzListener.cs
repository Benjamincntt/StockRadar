using Quartz;
using StockRadar.Application.Abstractions;

namespace StockRadar.Infrastructure.Scheduling;

/// <summary>
/// Ghi lần chạy cuối cho MỌI job theo lịch — key = <c>JobKey.Name</c> (trùng JobCatalog.JobId).
/// Tránh phải sửa từng job wrapper.
/// </summary>
internal sealed class JobStatusQuartzListener(IJobStatusService jobStatus) : IJobListener
{
    public string Name => "job-status-recorder";

    public Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task JobWasExecuted(
        IJobExecutionContext context,
        JobExecutionException? jobException,
        CancellationToken cancellationToken = default)
    {
        var finishedUtc = DateTime.UtcNow;
        var durationMs = (long)Math.Max(0, context.JobRunTime.TotalMilliseconds);
        var startedUtc = finishedUtc.AddMilliseconds(-durationMs);

        await jobStatus.RecordAsync(
            context.JobDetail.Key.Name,
            "schedule",
            startedUtc,
            finishedUtc,
            success: jobException is null,
            summary: null,
            error: jobException?.GetBaseException().Message);
    }
}
