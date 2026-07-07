using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

public interface IDailySessionSyncService
{
    Task<DailySessionSyncResultDto> RunAsync(CancellationToken cancellationToken = default);
}

public interface IUniverseRescreenService
{
    Task<UniverseRescreenResultDto> RunAsync(CancellationToken cancellationToken = default);
}

public interface IDailyAnalysisService
{
    /// <param name="runPostProcessing">Shadow mode, chấm tiêu chí, đo T+2.5 — tắt khi chạy tay từ UI để trả lời nhanh.</param>
    Task<DailyAnalysisResultDto> RunAsync(
        CancellationToken cancellationToken = default,
        bool runPostProcessing = true);
}

public interface IDailyOpportunityRepository
{
    Task ReplaceForDateAsync(
        DateOnly forTradingDate,
        IReadOnlyList<DailyOpportunityRecord> items,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyOpportunityRecord>> GetForDateAsync(
        DateOnly forTradingDate,
        CancellationToken cancellationToken = default);

    Task<DateOnly?> GetLatestForDateAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetSymbolsForDateAsync(
        DateOnly forTradingDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> GetScoresBySymbolsForDateAsync(
        DateOnly forTradingDate,
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpportunityTradeStateRow>> GetTradeStatesSinceAsync(
        DateOnly fromDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyOpportunityRecord>> GetSinceAsync(
        DateOnly fromDate,
        CancellationToken cancellationToken = default);
}

public sealed record OpportunityTradeStateRow(
    DateOnly ForTradingDate,
    string Symbol,
    string? TradeState,
    string? TradeStateReason);

public interface IDailyAnalysisRunRepository
{
    Task UpsertAsync(
        DateOnly forTradingDate,
        DateTime generatedAt,
        int stocksScored,
        int opportunitiesSaved,
        bool usedRelaxedFallback,
        CancellationToken cancellationToken = default);

    Task<DailyAnalysisRunRecord?> GetForDateAsync(
        DateOnly forTradingDate,
        CancellationToken cancellationToken = default);
}

public sealed record DailyAnalysisRunRecord(
    DateOnly ForTradingDate,
    DateTime GeneratedAt,
    int StocksScored,
    int OpportunitiesSaved,
    bool UsedRelaxedFallback = false);

public sealed record DailyOpportunityRecord(
    DateOnly ForTradingDate,
    int Rank,
    string Symbol,
    string Name,
    string Sector,
    int Score,
    decimal Price,
    decimal ChangePercent,
    decimal VolumeRatio,
    DateTime GeneratedAt,
    int? BuyScore = null,
    decimal? PredictedHitPercent = null,
    int? PredictedSampleCount = null,
    string? SetupDna = null,
    string? Recommendation = null,
    string? TradeState = null,
    string? TradeStateReason = null,
    string? EntryPointJson = null,
    string? ExplainJson = null);
