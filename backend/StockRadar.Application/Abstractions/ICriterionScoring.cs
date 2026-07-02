using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Abstractions;

public interface ICriterionScoringRepository
{
    Task<IReadOnlyDictionary<CriterionType, decimal>> GetWeightsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CriterionWeight>> GetWeightDetailsAsync(CancellationToken cancellationToken = default);

    Task UpsertWeightsAsync(
        IReadOnlyList<CriterionWeight> weights,
        CancellationToken cancellationToken = default);

    Task ReplaceDailyAccuracyAsync(
        DateOnly asOfDate,
        int horizon,
        IReadOnlyList<CriterionAccuracySnapshot> snapshots,
        DateTime generatedAt,
        CancellationToken cancellationToken = default);

    Task ReplaceGroupDailyAccuracyAsync(
        DateOnly asOfDate,
        int horizon,
        IReadOnlyList<CriterionGroupAccuracySnapshot> snapshots,
        DateTime generatedAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CriterionAccuracySnapshot>> GetDailyAccuracyAsync(
        DateOnly asOfDate,
        int horizon = 5,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CriterionGroupAccuracySnapshot>> GetGroupDailyAccuracyAsync(
        DateOnly asOfDate,
        int horizon = 5,
        CancellationToken cancellationToken = default);

    Task<DateOnly?> GetLatestAccuracyDateAsync(
        int horizon = 5,
        CancellationToken cancellationToken = default);

    /// <summary>Số ngày snapshot accuracy riêng biệt trong khoảng [from, to].</summary>
    Task<int> CountAccuracyDatesAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int horizon = 5,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CriterionAccuracySnapshot>> GetAccuracyRollingAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int horizon = 5,
        CancellationToken cancellationToken = default);

    /// <summary>Chuỗi snapshot theo từng ngày (phục vụ backtest trọng số reliability).</summary>
    Task<IReadOnlyList<CriterionAccuracyDailyPoint>> GetDailyAccuracySeriesAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int horizon = 5,
        CancellationToken cancellationToken = default);

    Task ReplaceStockScoresAsync(
        DateOnly asOfDate,
        IReadOnlyList<StockCriterionScoreRecord> scores,
        DateTime generatedAt,
        CancellationToken cancellationToken = default);

    Task ReplaceStockDetailsAsync(
        DateOnly asOfDate,
        int horizon,
        IReadOnlyList<StockCriterionDetailRecord> details,
        DateTime generatedAt,
        CancellationToken cancellationToken = default);

    Task UpsertWeeklyReviewsAsync(
        DateOnly weekStart,
        IReadOnlyList<WeeklyCriterionReviewSnapshot> criteria,
        IReadOnlyList<CriterionGroupWeeklySnapshot> groups,
        DateTime generatedAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WeeklyCriterionReviewSnapshot>> GetWeeklyReviewsAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CriterionGroupWeeklySnapshot>> GetGroupWeeklyReviewsAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken = default);

    Task<StockCriterionScoreRecord?> GetStockScoreAsync(
        DateOnly asOfDate,
        string symbol,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockCriterionScoreRecord>> GetTopStockScoresAsync(
        DateOnly asOfDate,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> GetCompositeScoresBySymbolsAsync(
        DateOnly asOfDate,
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken = default);
}

public interface IDailyCriterionScoringService
{
    Task<int> RunAfterAnalysisAsync(CancellationToken cancellationToken = default);

    /// <summary>Chấm ngược N ngày quá khứ (khung chính) để lấp đầy rolling window.</summary>
    Task<int> RunBackfillAsync(int days, CancellationToken cancellationToken = default);
}

public sealed record CriterionAccuracyDailyPoint(
    DateOnly AsOfDate,
    CriterionAccuracySnapshot Snapshot);
