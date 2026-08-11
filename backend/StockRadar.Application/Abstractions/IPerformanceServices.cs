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

public sealed record MasterAlertPositionRecord(
    Guid Id,
    string Symbol,
    DateOnly EntryDate,
    decimal EntryPrice,
    decimal PeakPriceSinceEntry,
    decimal CurrentPositionSize,
    IReadOnlyList<string> FiredAlertKinds,
    string? MarketPhaseAtEntry,
    bool IsClosed,
    DateOnly? ClosedDate,
    string? ExitRegime = null,
    decimal? OverheadBaseLow = null,
    decimal? OverheadBaseHigh = null,
    decimal? EntryBarLow = null,
    DateOnly? AnchorWindowStart = null,
    // --- Realized P&L (đợt 2) — xem PositionSellLegEntity/RealizedPnlMath. ---
    decimal MaxPositionSize = 0m,
    bool RealizedMeasured = false,
    DateTime? RealizedMeasuredAt = null,
    decimal? RealizedWeightedReturnPercent = null,
    decimal? RealizedReturnOnDeployedPercent = null,
    decimal? RealizedGrossReturnPercent = null,
    string? RealizedOutcomeBucket = null,
    string? RealizedStatus = null,
    string? RealizedFeeProfile = null,
    int? HoldingSessions = null);

/// <summary>1 nhịp bán (BanNua|BanHet) đọc lại từ DB — dùng để dựng <see cref="StockRadar.Domain.Services.SellLeg"/>.</summary>
public sealed record PositionSellLegRecord(
    Guid PositionId,
    string Signal,
    DateOnly SellDate,
    decimal SellPrice,
    decimal SoldSize,
    string PriceSource);

public interface IMasterAlertPositionRepository
{
    Task<IReadOnlyList<MasterAlertPositionRecord>> GetOpenPositionsAsync(CancellationToken ct = default);

    Task<MasterAlertPositionRecord?> GetOpenBySymbolAsync(string symbol, CancellationToken ct = default);

    /// <summary>
    /// BuyPoint1: tạo vị thế 0.5. BuyPoint2: nâng lên 1.0 (giữ EntryPrice/EntryDate gốc), hoặc tạo mới 1.0 nếu chưa có.
    /// Trả về id vị thế để publisher gắn <c>PositionId</c> vào SetupTrack.
    /// </summary>
    Task<Guid> UpsertOnBuyAsync(
        string symbol,
        DateOnly entryDate,
        decimal entryPrice,
        decimal positionSize,
        string firedKind,
        string? marketPhase,
        CancellationToken ct = default,
        string? exitRegime = null,
        decimal? overheadBaseLow = null,
        decimal? overheadBaseHigh = null,
        decimal? entryBarLow = null);

    /// <summary>Cập nhật đỉnh + append firedKind (không đụng size — dùng cho risk warning / theo dõi đỉnh mới).</summary>
    Task UpdatePeakAsync(
        Guid id,
        decimal peakPrice,
        string? appendFiredKind,
        CancellationToken ct = default);

    /// <summary>Phân loại / chuyển chế độ thoát lệnh.</summary>
    Task UpdateExitRegimeAsync(
        Guid id,
        string exitRegime,
        decimal? overheadBaseLow,
        decimal? overheadBaseHigh,
        DateOnly? anchorWindowStart,
        CancellationToken ct = default);

    /// <summary>
    /// Bán 1 nửa: <c>soldSize = CurrentPositionSize / 2</c> (halving thật, không hardcode).
    /// Insert leg BanNua, trừ size, append kind. Guard <c>soldSize &lt;= 0</c> → chỉ append kind, không insert
    /// (hot path Telegram VIP — không throw).
    /// </summary>
    Task RecordSellHalfAsync(
        Guid id,
        DateOnly sellDate,
        decimal sellPrice,
        DateTime firedAtUtc,
        string priceSource,
        CancellationToken ct = default);

    /// <summary>Bán hết: insert leg BanHet với <c>soldSize = CurrentPositionSize</c> hiện tại, rồi đóng vị thế.</summary>
    Task CloseAsync(
        Guid id,
        DateOnly closedDate,
        string appendFiredKind,
        decimal sellPrice,
        DateTime firedAtUtc,
        string priceSource,
        CancellationToken ct = default);

    /// <summary>
    /// Vị thế đã đóng cần đo/đo lại realized: chưa đo lần nào, hoặc <c>RealizedFeeProfile</c> không khớp
    /// <paramref name="feeProfileKey"/> hiện tại (đổi phí → auto-recompute).
    /// </summary>
    Task<IReadOnlyList<MasterAlertPositionRecord>> GetClosedPendingRealizedAsync(
        DateOnly fromEntryDate,
        string feeProfileKey,
        CancellationToken ct = default);

    /// <summary>Batch load sell legs theo danh sách position id (IN (...)) — tránh N+1.</summary>
    Task<IReadOnlyList<PositionSellLegRecord>> GetSellLegsAsync(
        IReadOnlyList<Guid> positionIds,
        CancellationToken ct = default);

    /// <summary>Lưu kết quả đo realized (hoặc đánh dấu MissingSellPrice) cho 1 vị thế.</summary>
    Task SaveRealizedAsync(
        Guid positionId,
        string status,
        string? feeProfileKey,
        decimal? weightedReturnPercent,
        decimal? returnOnDeployedPercent,
        decimal? grossReturnPercent,
        string? outcomeBucket,
        int? holdingSessions,
        CancellationToken ct = default);

    /// <summary>Vị thế đã đóng nhưng chưa dựng leg nào (chưa backfill giá bán) — dùng cho §6 backfill.</summary>
    Task<IReadOnlyList<MasterAlertPositionRecord>> GetClosedWithoutLegsAsync(
        DateOnly fromEntryDate,
        CancellationToken ct = default);

    /// <summary>Toàn bộ vị thế (mở + đóng) từ <paramref name="fromEntryDate"/> — dùng cho summary/realized-trades UI.</summary>
    Task<IReadOnlyList<MasterAlertPositionRecord>> GetPositionsSinceAsync(
        DateOnly fromEntryDate,
        CancellationToken ct = default);

    /// <summary>
    /// Backfill §6 Bước B: chèn 1 leg dựng lại (không đụng CurrentPositionSize — vị thế đã đóng, size đã = 0).
    /// Check-then-insert theo unique index (PositionId, Signal); trả false nếu đã tồn tại.
    /// </summary>
    Task<bool> InsertBackfillLegIfMissingAsync(
        Guid positionId,
        string symbol,
        string signal,
        DateOnly sellDate,
        decimal sellPrice,
        decimal soldSize,
        decimal remainingSizeAfter,
        string priceSource,
        DateTime firedAtUtc,
        CancellationToken ct = default);
}

public sealed record ShadowPickSeed(
    string Symbol,
    int Rank,
    int Score,
    decimal Price,
    decimal PredictedHitPercent);

public sealed record ShadowPickRecord(
    Guid Id,
    DateOnly ForTradingDate,
    int VariantMinPassScore,
    string Symbol,
    int Rank,
    int Score,
    decimal EntryPrice,
    decimal PredictedHitPercent,
    bool OutcomeMeasured,
    decimal? ForwardReturnPercent,
    string? OutcomeBucket,
    DateTime? MeasuredAt);

public sealed record ShadowVariantSummaryRecord(
    int VariantMinPassScore,
    int MeasuredCount,
    int GoodCount,
    int FlatCount,
    int FailedCount,
    decimal SuccessRatePercent,
    bool IsProduction,
    bool IsLeader,
    DateTime UpdatedAt);

public interface IShadowAnalysisRepository
{
    Task ReplacePicksForVariantAsync(
        DateOnly forTradingDate,
        int variantMinPassScore,
        IReadOnlyList<ShadowPickSeed> picks,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShadowPickRecord>> GetPendingOutcomesAsync(
        DateOnly measureThroughDate,
        CancellationToken cancellationToken = default);

    Task UpdateOutcomeAsync(
        Guid id,
        decimal forwardReturnPercent,
        string outcomeBucket,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShadowVariantSummaryRecord>> GetSummariesAsync(
        CancellationToken cancellationToken = default);

    Task RebuildSummariesAsync(
        int productionMinPassScore,
        int promoteAfterMeasuredCount,
        CancellationToken cancellationToken = default);

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

/// <summary>Backfill realized P&amp;L cho vị thế đã đóng trước khi có <c>PositionSellLegs</c> — xem plan §6.</summary>
public interface IRealizedPnlBackfillService
{
    Task<StockRadar.Application.DTOs.RealizedPnlBackfillResultDto> BackfillAsync(
        int days = 365,
        bool dryRun = false,
        CancellationToken ct = default);
}

public sealed record VipAlertFireRecord(
    Guid Id,
    string Symbol,
    DateOnly SessionDate,
    DateTime FiredAtUtc,
    string Signal,
    string? Branch,
    decimal FirePrice,
    decimal OpenPrice,
    decimal GainFromOpenPercent,
    decimal PacedVolumeRatio,
    decimal? MlProbAtFire,
    bool MlModelActive,
    int? BuyScore,
    decimal? PredictedHitPercent,
    string? MarketPhase,
    decimal? Rs5dPercent,
    decimal? AtrPercent,
    decimal? DistMa20Percent,
    decimal? Ma10,
    decimal? Ma20,
    decimal? Ma50,
    bool? UptrendLong,
    long? ForeignNet,
    long? PropNet,
    decimal? SessionPressure,
    string? VsaLabel,
    bool FeaturesComplete,
    bool IntradayMeasured,
    decimal? IntradayReturnPercent,
    decimal? IntradayMfePercent,
    decimal? IntradayMaePercent,
    decimal? SessionHighSinceFire,
    decimal? SessionLowSinceFire,
    string? LlmDecision = null,
    string? LlmReason = null,
    int? LlmLatencyMs = null,
    string? LlmModel = null,
    bool LlmShadowMode = false,
    string? SellContextJson = null);

public interface IVipAlertFireRepository
{
    Task AddAsync(VipAlertFireRecord fire, CancellationToken cancellationToken = default);

    /// <summary>Đã ghi fire cùng symbol+signal trong phiên — persist chống spam sau restart API.</summary>
    Task<bool> HasFiredAsync(
        string symbol,
        string signal,
        DateOnly sessionDate,
        CancellationToken cancellationToken = default);

    Task TouchSessionRangeAsync(
        string symbol,
        DateOnly sessionDate,
        decimal high,
        decimal low,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VipAlertFireRecord>> GetPendingIntradayAsync(
        DateOnly sessionDate,
        CancellationToken cancellationToken = default);

    Task MarkIntradayMeasuredAsync(
        Guid id,
        decimal closePrice,
        decimal? sessionHigh,
        decimal? sessionLow,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VipAlertFireRecord>> GetSinceAsync(
        DateOnly fromSessionDate,
        CancellationToken cancellationToken = default);

    /// <summary>Fires Bán 1 nửa/Bán hết trong khoảng [from, toInclusive] của 1 mã — dùng backfill giá bán §6.</summary>
    Task<IReadOnlyList<VipAlertFireRecord>> GetSellFiresInRangeAsync(
        string symbol,
        DateOnly from,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default);
}
