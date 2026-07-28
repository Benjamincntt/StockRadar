namespace StockRadar.Application.DTOs;

/// <summary>Trạng thái tổng hợp một job cho màn hình Jobs (mobile).</summary>
public sealed record JobStatusDto(
    string JobId,
    string Name,
    string Description,
    string Schedule,
    int FrequencyRank,
    bool Triggerable,
    string TriggerEndpoint,
    /// <summary>idle | success | failed</summary>
    string Status,
    DateTime? LastStartedAt,
    DateTime? LastFinishedAt,
    long? LastDurationMs,
    string? TriggeredBy,
    string? Summary,
    string? Error);
