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
        IReadOnlyList<CriterionAccuracySnapshot> snapshots,
        DateTime generatedAt,
        CancellationToken cancellationToken = default);

    Task ReplaceGroupDailyAccuracyAsync(
        DateOnly asOfDate,
        IReadOnlyList<CriterionGroupAccuracySnapshot> snapshots,
        DateTime generatedAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CriterionAccuracySnapshot>> GetDailyAccuracyAsync(
        DateOnly asOfDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CriterionGroupAccuracySnapshot>> GetGroupDailyAccuracyAsync(
        DateOnly asOfDate,
        CancellationToken cancellationToken = default);

    Task<DateOnly?> GetLatestAccuracyDateAsync(CancellationToken cancellationToken = default);

    /// <summary>Số ngày snapshot accuracy riêng biệt trong khoảng [from, to].</summary>
    Task<int> CountAccuracyDatesAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CriterionAccuracySnapshot>> GetAccuracyRollingAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);

    Task ReplaceStockScoresAsync(
        DateOnly asOfDate,
        IReadOnlyList<StockCriterionScoreRecord> scores,
        DateTime generatedAt,
        CancellationToken cancellationToken = default);

    Task ReplaceStockDetailsAsync(
        DateOnly asOfDate,
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
}
