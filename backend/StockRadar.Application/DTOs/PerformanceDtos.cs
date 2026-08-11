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
    EntryTimingSummaryDto? EntryTiming = null,
    RealizedPnlSummaryDto? Realized = null);

/// <summary>1 lệnh đã đóng, hiển thị chi tiết giá bán/lợi nhuận thực.</summary>
public record RealizedTradeDto(
    Guid PositionId,
    string Symbol,
    DateOnly EntryDate,
    decimal EntryPrice,
    DateOnly? ClosedDate,
    decimal MaxPositionSize,
    decimal? Sell1Price,
    DateOnly? Sell1Date,
    decimal? SellAllPrice,
    DateOnly? SellAllDate,
    decimal? WeightedReturnPercent,
    decimal? ReturnOnDeployedPercent,
    decimal? GrossReturnPercent,
    string? OutcomeBucket,
    string? Status,
    int? HoldingSessions,
    string? MarketPhaseAtEntry,
    string? ExitRegime);

public record RealizedTradesResponseDto(
    int Days,
    DateOnly FromDate,
    int TotalCount,
    IReadOnlyList<RealizedTradeDto> Trades);

/// <summary>
/// Tổng hợp lợi nhuận thực (realized P&amp;L) — tính từ <c>MasterAlertPositions</c> (1 dòng = 1 lệnh),
/// KHÔNG từ SetupTracks (tránh đếm trùng lệnh có cả MuaDiem1 + MuaDiem2).
/// </summary>
public record RealizedPnlSummaryDto(
    int ClosedTrades,
    int OpenTrades,
    int WinCount,
    int LoseCount,
    int FlatCount,
    decimal WinRatePercent,
    decimal? AvgRealizedReturnPercent,
    decimal? MedianRealizedReturnPercent,
    decimal? TotalWeightedReturnPercent,
    decimal? AvgHoldingSessions,
    RealizedTradeDto? BestTrade,
    RealizedTradeDto? WorstTrade,
    int MissingSellPriceCount,
    int ApproximateCount,
    decimal BuyFeePercent,
    decimal SellFeePercent,
    decimal SellTaxPercent,
    decimal WinThresholdPercent,
    string MethodologyNote);

/// <summary>Kết quả backfill realized P&amp;L cho vị thế đóng trước khi có <c>PositionSellLegs</c> — xem plan §6.</summary>
public record RealizedPnlBackfillResultDto(
    int Days,
    DateOnly FromDate,
    int ClosedPositionsScanned,
    int TracksLinked,
    int AmbiguousTracks,
    int LegsFromFires,
    int LegsFromForwardT25,
    int ApproximatePositions,
    int MissingSellPricePositions,
    int Measured,
    bool DryRun,
    string Summary);

/// <summary>North Star — hit T+2.5 theo rank lúc vào list (Phase 1 baseline).</summary>
public record OpportunityNorthStarReportDto(
    DateOnly FromDate,
    DateOnly ToDate,
    int MeasuredSetups,
    decimal SuccessThresholdPercent,
    IReadOnlyList<OpportunityRankBucketMetricsDto> RankBuckets,
    IReadOnlyList<OpportunityTradeStateMetricsDto> TradeStateBuckets,
    string MethodologyNote);

public record OpportunityRankBucketMetricsDto(
    string BucketId,
    int MaxRank,
    int MeasuredCount,
    int GoodCount,
    int FlatCount,
    int FailedCount,
    decimal HitRatePercent,
    decimal AvgReturnT25Percent,
    decimal? AvgMfePercent,
    decimal? AvgMaePercent,
    int SwingSamples);

public record OpportunityTradeStateMetricsDto(
    string TradeState,
    string TradeStateLabelVi,
    int MeasuredCount,
    int GoodCount,
    decimal HitRatePercent,
    decimal AvgReturnT25Percent,
    decimal? AvgMfePercent,
    decimal? AvgMaePercent,
    int SwingSamples);

/// <summary>Độ chính xác noti VIP BuyPoint — intraday + join T+2.5 SetupTrack master.</summary>
public record VipAlertAccuracyReportDto(
    DateOnly FromDate,
    DateOnly ToDate,
    int TotalFires,
    int IntradayMeasured,
    decimal AvgIntradayReturnPercent,
    decimal IntradayHitRatePercent,
    int MasterT25Measured,
    decimal MasterT25HitRatePercent,
    IReadOnlyList<VipAlertBucketMetricsDto> ByBranch,
    IReadOnlyList<VipAlertBucketMetricsDto> ByMarketPhase,
    IReadOnlyList<VipAlertBucketMetricsDto> ByMlProbBucket,
    string MethodologyNote);

public record VipAlertBucketMetricsDto(
    string BucketId,
    int FireCount,
    int IntradayMeasured,
    decimal IntradayHitRatePercent,
    decimal AvgIntradayReturnPercent,
    int? MasterT25Measured,
    decimal? MasterT25HitRatePercent);

/// <summary>Lịch sử lệnh Top/Mua + đúng/sai T+2.5.</summary>
public enum MeasurementStatus
{
    Pending,
    Measured
}

public record AlertHistoryResponseDto(
    decimal OverallSuccessRatePercent,
    int TotalMeasured,
    int TotalSuccess,
    int TotalFailed,
    int TotalFlat,
    int TotalPending,
    int TotalTracked,
    IReadOnlyList<AlertHistoryItemDto> Alerts,
    // --- Realized P&L aggregate (từ MasterAlertPositions, 1 dòng = 1 lệnh) — xem AlertHistoryPage. ---
    int TotalClosedTrades = 0,
    int TotalOpenTrades = 0,
    int RealizedWinCount = 0,
    int RealizedLoseCount = 0,
    int RealizedFlatCount = 0,
    decimal RealizedWinRatePercent = 0m,
    decimal? AvgRealizedReturnPercent = null);

public record AlertHistoryItemDto(
    Guid Id,
    string Symbol,
    DateOnly EntryDate,
    decimal EntryPrice,
    string AlertType,
    string AlertTypeLabel,
  /// <summary>Thời điểm phát lệnh — ISO 8601 kèm +07:00 (đóng cửa phiên VN).</summary>
    DateTimeOffset AlertIssuedAt,
    MeasurementStatus Status,
    decimal? ForwardPriceT25,
    decimal? ForwardReturnPercent,
    bool? IsSuccess,
    string? OutcomeBucket,
    DateTime? MeasuredAt,
    // --- Realized P&L (giá bán thật/gần đúng) — null khi track không gắn PositionId (vd TopCoHoi). ---
    Guid? PositionId = null,
    /// <summary><c>Closed</c>|<c>Open</c>|<c>None</c> (không có vị thế — track TopCoHoi).</summary>
    string PositionStatus = "None",
    decimal? RealizedReturnPercent = null,
    decimal? RealizedWeightedReturnPercent = null,
    string? RealizedOutcomeBucket = null,
    bool? RealizedIsSuccess = null,
    decimal? Sell1Price = null,
    DateOnly? Sell1Date = null,
    decimal? SellAllPrice = null,
    DateOnly? SellAllDate = null,
    int? HoldingSessions = null,
    string? RealizedStatus = null);

public record AlertHistoryTrendBucketDto(
    string BucketId,
    string PeriodLabel,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal WinRatePercent,
    decimal? DeltaWinRatePercent,
    int WinCount,
    int LoseCount,
    int FlatCount,
    int PendingCount,
    int DecidedCount,
    bool IsSmallSample,
    bool IsCurrentPeriod,
    decimal? AvgReturnPercent,
    // --- Realized P&L song song T+2.5 — đếm theo PositionId duy nhất trong bucket (tránh đếm trùng
    // MuaDiem1+MuaDiem2). Trade-off: bucket theo EntryDate nên P&L thực hiện (ClosedDate) có thể thuộc kỳ sau. ---
    int RealizedClosedCount = 0,
    int RealizedWinCount = 0,
    int RealizedLoseCount = 0,
    int RealizedFlatCount = 0,
    decimal RealizedWinRatePercent = 0m,
    decimal? AvgRealizedReturnPercent = null);

public record AlertHistoryTrendsResponseDto(
    string Period,
    AlertHistoryTrendBucketDto? SelectedBucket,
    IReadOnlyList<AlertHistoryTrendBucketDto> Buckets);
