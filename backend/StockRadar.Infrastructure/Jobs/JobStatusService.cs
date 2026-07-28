using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Jobs;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Entities;

namespace StockRadar.Infrastructure.Jobs;

/// <summary>
/// Ghi (upsert 1 dòng/job) và truy vấn trạng thái pipeline job.
/// Singleton + tự mở scope DbContext cho mỗi lần ghi để không giữ connection suốt job dài.
/// Việc ghi trạng thái không bao giờ làm hỏng job thật (nuốt lỗi, chỉ log).
/// </summary>
internal sealed class JobStatusService(
    IServiceScopeFactory scopeFactory,
    ILogger<JobStatusService> logger) : IJobStatusService
{
    public async Task<IReadOnlyList<JobStatusDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        Dictionary<string, JobRunStatusEntity> rows;
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            rows = await db.JobRunStatuses
                .AsNoTracking()
                .ToDictionaryAsync(x => x.JobId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không đọc được trạng thái job — trả metadata rỗng.");
            rows = new Dictionary<string, JobRunStatusEntity>();
        }

        return JobCatalog.All
            .OrderBy(d => d.FrequencyRank)
            .Select(d =>
            {
                rows.TryGetValue(d.JobId, out var r);
                return new JobStatusDto(
                    d.JobId,
                    d.Name,
                    d.Description,
                    d.Schedule,
                    d.FrequencyRank,
                    d.Triggerable,
                    d.TriggerEndpoint,
                    Status: r?.Status is { Length: > 0 } s ? s : "idle",
                    LastStartedAt: r?.LastStartedAt,
                    LastFinishedAt: r?.LastFinishedAt,
                    LastDurationMs: r?.LastDurationMs,
                    TriggeredBy: r?.TriggeredBy,
                    Summary: r?.Summary,
                    Error: r?.Error);
            })
            .ToList();
    }

    public async Task<T> TrackAsync<T>(
        string jobId,
        string triggeredBy,
        Func<CancellationToken, Task<T>> work,
        Func<T, string?>? summarize = null,
        CancellationToken cancellationToken = default)
    {
        var startedUtc = DateTime.UtcNow;
        try
        {
            var result = await work(cancellationToken);
            await RecordAsync(jobId, triggeredBy, startedUtc, DateTime.UtcNow, true, summarize?.Invoke(result), null);
            return result;
        }
        catch (Exception ex)
        {
            await RecordAsync(jobId, triggeredBy, startedUtc, DateTime.UtcNow, false, null, ex.GetBaseException().Message);
            throw;
        }
    }

    public async Task RecordAsync(
        string jobId,
        string triggeredBy,
        DateTime startedUtc,
        DateTime finishedUtc,
        bool success,
        string? summary,
        string? error)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await db.JobRunStatuses.FirstOrDefaultAsync(x => x.JobId == jobId);
            if (row is null)
            {
                row = new JobRunStatusEntity { JobId = jobId };
                db.JobRunStatuses.Add(row);
            }

            row.Status = success ? "success" : "failed";
            row.TriggeredBy = triggeredBy;
            row.LastStartedAt = startedUtc;
            row.LastFinishedAt = finishedUtc;
            row.LastDurationMs = (long)Math.Max(0, (finishedUtc - startedUtc).TotalMilliseconds);
            row.Summary = Truncate(summary, 512);
            row.Error = Truncate(error, 1024);

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không ghi được trạng thái job {JobId}.", jobId);
        }
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
