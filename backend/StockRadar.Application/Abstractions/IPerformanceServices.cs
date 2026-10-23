namespace StockRadar.Application.Abstractions;

public sealed record SetupTrackRecord(
    Guid Id,
    string Symbol,
    string SourceType,
    DateOnly EntryDate,
    decimal EntryPrice,
    DateOnly? OpportunityForDate,
    int? OpportunityRank,
    int? OpportunityScore,
    decimal? SessionChangePercent,
    long? SessionVolume,
    decimal? PeakGainPercent,
    bool OutcomeMeasured,
    decimal? ForwardPriceT25,
    decimal? ForwardReturnPercent,
    string? OutcomeBucket,
    DateTime? MeasuredAt,
    DateOnly? WeekStartDate,
    decimal? PredictedHitPercent = null,
    string? SetupDna = null,
    string? ScoreBreakdownJson = null,
    decimal? ForwardReturnT5 = null,
    decimal? ForwardReturnT10 = null,
    string? OutcomeBucketT5 = null,
    string? OutcomeBucketT10 = null,
    decimal? MaxFavorableExcursionPercent = null,
    decimal? MaxAdverseExcursionPercent = null,
    bool SwingMetricsMeasured = false,
    bool? HadMasterConfirm = null,
    string? TradeState = null,
    string? TradeStateReason = null,
    Guid? PositionId = null,
    // --- Projection-only: sinh ra từ LEFT JOIN MasterAlertPositions/PositionSellLegs khi đọc alert-history,
    // KHÔNG phải cột DB của SetupTracks. Null với mọi track khác (không qua GetAlertHistoryAsync/Tracks). ---
    bool? PositionIsClosed = null,
    decimal? RealizedReturnPercent = null,
    decimal? RealizedWeightedReturnPercent = null,
    string? RealizedOutcomeBucket = null,
    string? RealizedStatus = null,
    int? HoldingSessions = null,
    decimal? Sell1Price = null,
    DateOnly? Sell1Date = null,
    decimal? SellAllPrice = null,
    DateOnly? SellAllDate = null);

public sealed record WeeklyOpportunityReviewRecord(
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

public interface ISetupTrackRepository
{
    Task AddAsync(SetupTrackRecord track, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string symbol,
        string sourceType,
        DateOnly entryDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SetupTrackRecord>> GetPendingOutcomesAsync(
        DateOnly measureThroughDate,
        CancellationToken cancellationToken = default);

    Task UpdateOutcomeAsync(
        Guid id,
        decimal forwardPriceT25,
        decimal forwardReturnPercent,
        string outcomeBucket,
        DateOnly weekStart,
        bool? hadMasterConfirm,
        CancellationToken cancellationToken = default);

    /// <summary>Đã đo T+2.5 và có ForwardReturnPercent — dùng reclassify khi đổi ngưỡng Win/Flat.</summary>
    Task<IReadOnlyList<SetupTrackRecord>> GetMeasuredWithForwardReturnAsync(
        CancellationToken cancellationToken = default);

    Task UpdateOutcomeBucketAsync(
        Guid id,
        string outcomeBucket,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SetupTrackRecord>> GetPendingSwingMetricsAsync(
        DateOnly measureThroughDate,
        CancellationToken cancellationToken = default);

    Task UpdateSwingMetricsAsync(
        Guid id,
        decimal? forwardReturnT5,
        decimal? forwardReturnT10,
        string? outcomeBucketT5,
        string? outcomeBucketT10,
        decimal maxFavorableExcursionPercent,
        decimal maxAdverseExcursionPercent,
        CancellationToken cancellationToken = default);

    Task<bool> HasMasterConfirmAsync(
        string symbol,
        DateOnly entryDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SetupTrackRecord>> GetMeasuredOpportunitiesForEntryTimingAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SetupTrackRecord>> GetForWeekAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, SetupTrackRecord>> GetOpportunityMapForDateAsync(
        DateOnly forTradingDate,
        CancellationToken cancellationToken = default);

    Task RegisterOpportunitiesAsync(
        DateOnly forTradingDate,
        IReadOnlyList<OpportunityTrackSeed> seeds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SetupTrackRecord>> GetMeasuredWithPredictionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SetupTrackRecord>> GetMeasuredOpportunitySetupsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SetupTrackRecord>> GetMeasuredOpportunitiesSinceAsync(
        DateOnly fromEntryDate,
        CancellationToken cancellationToken = default);

    Task<(int Measured, int Good)> GetMeasuredOpportunityCountsSinceAsync(
        DateOnly fromEntryDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lịch sử Top/Mua điểm + aggregate đúng/sai T+2.5.
    /// Aggregates tính trên toàn bộ filter; Alerts là trang skip/limit.
    /// </summary>
    Task<AlertHistoryPage> GetAlertHistoryAsync(
        int limit,
        int skip,
        bool? outcomeMeasured,
        string? sourceType,
        bool buyPointsOnly,
        DateOnly? fromEntryDate = null,
        DateOnly? toEntryDateInclusive = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SetupTrackRecord>> GetAlertHistoryTracksAsync(
        bool buyPointsOnly,
        string? sourceType,
        CancellationToken cancellationToken = default);

    /// <summary>Track Mua điểm 1/2 chưa gắn PositionId — dùng backfill §6 Bước A.</summary>
    Task<IReadOnlyList<SetupTrackRecord>> GetUnlinkedBuyTracksSinceAsync(
        DateOnly fromEntryDate,
        CancellationToken cancellationToken = default);

    /// <summary>Gắn PositionId cho 1 track — dùng backfill §6 Bước A.</summary>
    Task SetPositionIdAsync(
        Guid trackId,
        Guid positionId,
        CancellationToken cancellationToken = default);
}

public sealed record AlertHistoryPage(
    int TotalTracked,
    int TotalMeasured,
    int TotalSuccess,
    int TotalFailed,
    int TotalFlat,
    int TotalPending,
    IReadOnlyList<SetupTrackRecord> Alerts,
    // --- Realized P&L aggregate — tính riêng từ MasterAlertPositions (1 dòng = 1 lệnh), KHÔNG từ SetupTracks,
    // để tránh đếm trùng lệnh có cả MuaDiem1 + MuaDiem2. Xem OutcomeBucketNames cho RealizedWin/Lose/Flat. ---
    int TotalClosedTrades = 0,
    int TotalOpenTrades = 0,
    int RealizedWinCount = 0,
    int RealizedLoseCount = 0,
    int RealizedFlatCount = 0,
    decimal RealizedWinRatePercent = 0m,
    decimal? AvgRealizedReturnPercent = null);

public sealed record ShadowPickSeed(
    string Symbol,
    int Rank,
    int Score,
    decimal Price,
    decimal PredictedHitPercent);

public interface IShadowAnalysisRepository
{
    Task ReplaceWeightPicksAsync(
        DateOnly forTradingDate,
        decimal weightMultiplier,
        IReadOnlyList<ShadowPickSeed> picks,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShadowWeightPickRecord>> GetPendingWeightOutcomesAsync(
        DateOnly measureThroughDate,
        CancellationToken cancellationToken = default);

    Task UpdateWeightOutcomeAsync(
        Guid id,
        decimal forwardReturnPercent,
        string outcomeBucket,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShadowWeightSummaryRecord>> GetWeightSummariesAsync(
        CancellationToken cancellationToken = default);

    Task RebuildWeightSummariesAsync(
        decimal productionMultiplier,
        int promoteAfterMeasuredCount,
        CancellationToken cancellationToken = default);
}

public sealed record ShadowWeightPickRecord(
    Guid Id,
    DateOnly ForTradingDate,
    decimal WeightMultiplier,
    string Symbol,
    int Rank,
    int Score,
    decimal EntryPrice,
    decimal PredictedHitPercent,
    bool OutcomeMeasured,
    decimal? ForwardReturnPercent,
    string? OutcomeBucket,
    DateTime? MeasuredAt);

public sealed record ShadowWeightSummaryRecord(
    decimal WeightMultiplier,
    int MeasuredCount,
    int GoodCount,
    int FlatCount,
    int FailedCount,
    decimal SuccessRatePercent,
    bool IsProduction,
    bool IsLeader,
    DateTime UpdatedAt);

public sealed record EntryTimingStateRecord(
    int TopOnlyMeasured,
    int TopOnlyGood,
    int ConfirmMeasured,
    int ConfirmGood,
    bool PreferMasterConfirm,
    DateTime UpdatedAt);

public interface IEntryTimingRepository
{
    Task<EntryTimingStateRecord?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(EntryTimingStateRecord state, CancellationToken cancellationToken = default);
}

public sealed record TradeJournalRecord(
    Guid Id,
    Guid UserId,
    string Symbol,
    DateOnly TradeDate,
    string Action,
    decimal? SizePercent,
    string? EngineVerdict,
    string? Note,
    int? BuyScore,
    decimal? PredictedHit,
    string? SetupDna,
    DateTime CreatedAt);

public sealed record PersonalCalibrationRecord(
    decimal Factor,
    int SampleCount,
    DateTime UpdatedAt);

public interface ITradeJournalRepository
{
    Task AddAsync(TradeJournalRecord entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TradeJournalRecord>> GetForUserAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<PersonalCalibrationRecord?> GetCalibrationAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task SaveCalibrationAsync(
        Guid userId,
        PersonalCalibrationRecord calibration,
        CancellationToken cancellationToken = default);
}

public sealed record OpportunityTrackSeed(
    string Symbol,
    int Rank,
    int Score,
    decimal Price,
    decimal ChangePercent,
    decimal PredictedHitPercent = 0,
    string? SetupDna = null,
    string? ScoreBreakdownJson = null,
    string? TradeState = null,
    string? TradeStateReason = null);

public interface IWeeklyOpportunityReviewRepository
{
    Task UpsertAsync(WeeklyOpportunityReviewRecord review, CancellationToken cancellationToken = default);

    Task<WeeklyOpportunityReviewRecord?> GetLatestAsync(CancellationToken cancellationToken = default);

    Task<WeeklyOpportunityReviewRecord?> GetForWeekAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken = default);
}

public interface IOpportunityPerformanceService
{
    Task<int> MeasurePendingOutcomesAsync(CancellationToken cancellationToken = default);

    Task<WeeklyOpportunityReviewRecord?> RunWeeklyReviewAsync(
        DateOnly? weekStart = null,
        CancellationToken cancellationToken = default);
}
