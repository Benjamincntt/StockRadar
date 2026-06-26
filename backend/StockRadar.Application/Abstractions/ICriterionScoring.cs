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
}

public sealed record StockCriterionScoreRecord(
    DateOnly AsOfDate,
    string Symbol,
    int CompositeScore,
    decimal NextDayChangePercent,
    IReadOnlyList<CriterionScore> Scores);

public interface IDailyCriterionScoringService
{
    Task<int> RunAfterAnalysisAsync(CancellationToken cancellationToken = default);
}
