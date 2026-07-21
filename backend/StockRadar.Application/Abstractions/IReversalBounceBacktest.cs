namespace StockRadar.Application.Abstractions;

public sealed record ReversalBounceBacktestRequest(
    DateOnly From,
    DateOnly To,
    decimal? MinScoreOverride = null,
    bool AllowDefensiveEarlyExit = false);

public sealed record ReversalBounceBacktestTradeRecord(
    string Symbol,
    DateOnly SignalDate,
    DateOnly EntryDate,
    decimal EntryPrice,
    decimal? ExitPrice,
    DateOnly? ExitDate,
    int SessionsToExit,
    string ExitReason,
    decimal ReturnPercentGross,
    decimal ReturnPercentNet,
    decimal MaxFavorablePercent,
    decimal MaxAdversePercent,
    decimal TotalScore,
    string Regime);

public sealed record ReversalBounceBacktestReport(
    DateOnly From,
    DateOnly To,
    int TotalSetups,
    int EnteredTrades,
    int ExitedTrades,
    int FloorLockDeferredCount,
    int GapCancelledCount,
    int WinCount,
    int FlatCount,
    int LoseCount,
    decimal WinRatePercent,
    decimal AvgReturnPercentGross,
    decimal AvgReturnPercentNet,
    decimal AvgMfePercent,
    decimal AvgMaePercent,
    IReadOnlyList<ReversalBounceBacktestTradeRecord> Trades);

public interface IReversalBounceBacktestService
{
    Task<ReversalBounceBacktestReport> RunAsync(
        ReversalBounceBacktestRequest request,
        CancellationToken cancellationToken = default);
}
