using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

/// <summary>Ghi lại và truy vấn trạng thái/lần chạy cuối của các pipeline job.</summary>
public interface IJobStatusService
{
    /// <summary>Danh sách toàn bộ job (theo <c>JobCatalog</c>) kèm lần chạy cuối, xếp theo tần suất.</summary>
    Task<IReadOnlyList<JobStatusDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Bọc một lần thực thi job: ghi kết quả (success/failed + duration) khi xong.
    /// KHÔNG nuốt exception — ném lại nguyên trạng sau khi đã ghi trạng thái Failed.
    /// </summary>
    Task<T> TrackAsync<T>(
        string jobId,
        string triggeredBy,
        Func<CancellationToken, Task<T>> work,
        Func<T, string?>? summarize = null,
        CancellationToken cancellationToken = default);

    /// <summary>Ghi trực tiếp một lần chạy đã hoàn tất (dùng cho Quartz job listener). Không bao giờ ném.</summary>
    Task RecordAsync(
        string jobId,
        string triggeredBy,
        DateTime startedUtc,
        DateTime finishedUtc,
        bool success,
        string? summary,
        string? error);
}
