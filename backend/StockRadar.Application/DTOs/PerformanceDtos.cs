namespace StockRadar.Application.DTOs;

public record SetupTrackDto(
    Guid Id,
    string Symbol,
    string SourceType,
    string SourceLabel,
    DateOnly EntryDate,
    decimal EntryPrice,
    int? OpportunityRank,
    int? OpportunityScore,
    decimal? SessionChangePercent,
    decimal? ForwardReturnPercent,
    string? OutcomeBucket,
    DateTime? MeasuredAt,
    decimal? PredictedHitPercent = null,
    string? SetupDna = null,
    decimal? ForwardReturnT5 = null,
    decimal? ForwardReturnT10 = null,
    string? OutcomeBucketT5 = null,
    string? OutcomeBucketT10 = null,
    decimal? MaxFavorableExcursionPercent = null,
    decimal? MaxAdverseExcursionPercent = null,
    bool? HadMasterConfirm = null);

public record ShadowWeightVariantStatusDto(
    decimal WeightMultiplier,
    int MeasuredCount,
    decimal SuccessRatePercent,
    bool IsProduction,
    bool IsLeader);

public record EntryTimingSummaryDto(
    decimal TopOnlySuccessRate,
    decimal ConfirmSuccessRate,
    int TopOnlySamples,
    int ConfirmSamples,
    bool PreferMasterConfirm);

public record HitCalibrationBucketDto(
    string BucketId,
    int SampleCount,
    decimal PredictedMidPercent,
    decimal ActualHitRatePercent,
    decimal CalibrationFactor);

public record HitCalibrationSummaryDto(
    decimal GlobalFactor,
    int TotalSamples,
    decimal PredictionBiasPercent,
    DateTime? UpdatedAt,
    IReadOnlyList<HitCalibrationBucketDto> Buckets);

public record FalsePositiveMiningSummaryDto(
    int FalsePositiveSetups,
    int GoodSetups,
    IReadOnlyList<FalsePositiveCriterionDto> FlaggedCriteria);

public record FalsePositiveCriterionDto(
    string ComponentId,
    string Label,
    int FalsePositiveHits,
    decimal FalsePositiveAvgNorm,
    decimal GoodAvgNorm,
    decimal DeceptionScore,
    decimal WeightPenalty);

public record WeeklyOpportunityReviewDto(
    DateOnly WeekStartDate,
    int TotalTracked,
    int MeasuredCount,
    int GoodCount,
    int FlatCount,
    int FailedCount,
    decimal SuccessRatePercent,
    decimal FailedRatePercent,
    int OpportunityCount,
    int BuyPoint1Count,
    int BuyPoint2Count,
    int CutLoss1Count,
    int CutAllCount,
    decimal OpportunitySuccessRate,
    decimal BuyPoint1SuccessRate,
    decimal BuyPoint2SuccessRate,
    string RecommendedAction,
    string Summary,
    DateTime GeneratedAt);

public record OpportunityPerformanceSummaryDto(
    DateOnly? WeekStartDate,
    DateTime? GeneratedAt,
    WeeklyOpportunityReviewDto? WeeklyReview,
    IReadOnlyList<SetupTrackDto> RecentOutcomes,
    string? StatusMessage,
    HitCalibrationSummaryDto? Calibration = null,
    FalsePositiveMiningSummaryDto? FalsePositiveMining = null,
    IReadOnlyList<ShadowVariantStatusDto>? ShadowVariants = null,
    string? ShadowStatusMessage = null,
    IReadOnlyList<ShadowWeightVariantStatusDto>? ShadowWeightVariants = null,
    EntryTimingSummaryDto? EntryTiming = null);
