using StockRadar.Domain.Entities;
using StockRadar.Domain.Services.ReversalBounce;

namespace StockRadar.Application.Abstractions;

/// <summary>
/// Snapshot ứng viên counter-trend (persistence-facing record; EF entity map từ đây).
/// Bất biến theo <c>(Symbol, TradingDate, StrategyVersion, SetupId)</c>.
/// </summary>
public sealed record ReversalCandidateSnapshot(
    DateOnly TradingDate,
    string Symbol,
    ReversalBounceStage Stage,
    Guid SetupId,
    DateOnly? CapitulationDate,
    decimal? CapitulationLow,
    decimal? CapitulationClose,
    int RecoveryAttemptCount,
    ReversalBounceComponentScores ComponentScores,
    decimal TotalScore,
    MarketRegime MarketRegime,
    bool IsActionable,
    ReversalBounceTradePlan? TradePlan,
    string StrategyVersion,
    string AlgorithmParametersHash,
    int SchemaVersion,
    Guid RunBatchId,
    IReadOnlyList<ReversalBounceReason> Reasons,
    DateTime CreatedAtUtc);

/// <summary>Repo snapshot ReversalBounce (idempotent upsert theo unique key).</summary>
public interface IReversalCandidateSnapshotRepository
{
    Task UpsertAsync(ReversalCandidateSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReversalCandidateSnapshot>> GetForDateAsync(
        DateOnly tradingDate, bool? actionableOnly = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReversalCandidateSnapshot>> GetHistoryAsync(
        string symbol, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>Toàn bộ snapshot actionable trong [from, to] (mọi mã) — cho shadow report Phase 1.</summary>
    Task<IReadOnlyList<ReversalCandidateSnapshot>> GetActionableInRangeAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task<int> CountSameSetupPriorAsync(
        string symbol, Guid setupId, string strategyVersion, DateOnly beforeDate,
        CancellationToken cancellationToken = default);
}

/// <summary>Kết quả 1 lần chạy analyzer counter-trend cho 1 phiên.</summary>
public sealed record ReversalBounceAnalysisResult(
    Guid RunBatchId,
    DateOnly TradingDate,
    int UniverseScanned,
    int SnapshotsWritten,
    int ActionableCount);

/// <summary>Chạy analyzer counter-trend cho một phiên (gọi cuối daily pipeline sau breadth).</summary>
public interface IReversalBounceAnalysisService
{
    Task<ReversalBounceAnalysisResult> RunAsync(
        DateOnly forTradingDate,
        IReadOnlyList<Stock> universe,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phân tích on-demand 1 mã (kể cả Stage=None) — dùng cho màn chi tiết cổ phiếu.
    /// Không ghi snapshot. Trả null nếu không tìm thấy mã.
    /// </summary>
    Task<ReversalCandidateSnapshot?> AnalyzeSymbolLiveAsync(
        string symbol,
        CancellationToken cancellationToken = default);
}
