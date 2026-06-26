namespace StockRadar.Application.DTOs;

public sealed record HistoryBackfillRequest(
    string[]? Groups = null,
    string? StartDate = null,
    /// <summary>Mặc định T-1 (phiên giao dịch trước ngày chạy Job 2 đầu tiên).</summary>
    string? EndDate = null,
    /// <summary>fast = nút thủ công; night = chạy đêm (delay lớn hơn).</summary>
    string Mode = "fast");

public sealed record HistoryBackfillResultDto(
    int SymbolsTotal,
    int SymbolsScreened,
    int SymbolsInUniverse,
    int SymbolsSucceeded,
    int SymbolsFailed,
    int SymbolsExcluded,
    int BarsWritten,
    IReadOnlyList<string> FailedSymbols,
    DateTime CompletedAt);

public sealed record HistoryBackfillStatusDto(
    bool IsRunning,
    string? CurrentSymbol,
    int Processed,
    int Total,
    DateTime? StartedAt)
{
    public int PercentComplete => Total > 0 ? Processed * 100 / Total : 0;
}
