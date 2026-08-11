namespace StockRadar.Infrastructure.Persistence.Entities;

public sealed class SetupTrackEntity
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = "";
    public string SourceType { get; set; } = "";
    public DateOnly EntryDate { get; set; }
    public decimal EntryPrice { get; set; }

    /// <summary>Liên kết tới vị thế Master Alert (MuaDiem1/MuaDiem2) — null với track TopCoHoi (không phải vị thế).</summary>
    public Guid? PositionId { get; set; }
    public DateOnly? OpportunityForDate { get; set; }
    public int? OpportunityRank { get; set; }
    public int? OpportunityScore { get; set; }
    public decimal? SessionChangePercent { get; set; }
    public long? SessionVolume { get; set; }
    public decimal? PeakGainPercent { get; set; }
    public bool OutcomeMeasured { get; set; }
    public decimal? ForwardPriceT25 { get; set; }
    public decimal? ForwardReturnPercent { get; set; }
    public string? OutcomeBucket { get; set; }
    public DateTime? MeasuredAt { get; set; }
    public DateOnly? WeekStartDate { get; set; }
    public decimal? PredictedHitPercent { get; set; }
    public string? SetupDna { get; set; }
    public string? ScoreBreakdownJson { get; set; }
    public decimal? ForwardReturnT5 { get; set; }
    public decimal? ForwardReturnT10 { get; set; }
    public string? OutcomeBucketT5 { get; set; }
    public string? OutcomeBucketT10 { get; set; }
    public decimal? MaxFavorableExcursionPercent { get; set; }
    public decimal? MaxAdverseExcursionPercent { get; set; }
    public bool SwingMetricsMeasured { get; set; }
    public bool? HadMasterConfirm { get; set; }
    public string? TradeState { get; set; }
    public string? TradeStateReason { get; set; }
}

/// <summary>Vị thế live Master Alert VIP — settlement-aware, khác SetupTrack (đo T+2.5/North Star).</summary>
public sealed class MasterAlertPositionEntity
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = "";
    public DateOnly EntryDate { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal PeakPriceSinceEntry { get; set; }
    public decimal CurrentPositionSize { get; set; }
    public string FiredAlertKindsJson { get; set; } = "[]";
    public string? MarketPhaseAtEntry { get; set; }
    public bool IsClosed { get; set; }
    public DateOnly? ClosedDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary><c>UnderBase</c> | <c>BlueSky</c>; null = vị thế cũ chưa phân loại.</summary>
    public string? ExitRegime { get; set; }

    /// <summary>Cạnh dưới nền trên — mục tiêu chốt lãi khi <see cref="ExitRegime"/> = UnderBase.</summary>
    public decimal? OverheadBaseLow { get; set; }

    /// <summary>Cạnh trên nền trên — mốc xác định vượt nền để chuyển chế độ.</summary>
    public decimal? OverheadBaseHigh { get; set; }

    /// <summary>Giá thấp nhất phiên mở vị thế — mốc phủ nhận cây vượt đỉnh.</summary>
    public decimal? EntryBarLow { get; set; }

    /// <summary>Phiên sớm nhất được tính vào mốc tham chiếu.</summary>
    public DateOnly? AnchorWindowStart { get; set; }

    /// <summary>Size lớn nhất từng đạt được (1.0 nếu có Mua điểm 2) — cần vì <see cref="CurrentPositionSize"/> bị set 0 khi đóng.</summary>
    public decimal MaxPositionSize { get; set; }

    /// <summary>Đã đo realized P&amp;L (giá bán thật/gần đúng) cho vị thế đã đóng này.</summary>
    public bool RealizedMeasured { get; set; }
    public DateTime? RealizedMeasuredAt { get; set; }

    /// <summary>Tổng đóng góp NAV (Σ size × return mỗi nhịp bán) — KHÔNG normalize.</summary>
    public decimal? RealizedWeightedReturnPercent { get; set; }

    /// <summary>% lợi nhuận trên vốn thực triển khai — dùng để Classify Good/Flat/Failed.</summary>
    public decimal? RealizedReturnOnDeployedPercent { get; set; }

    /// <summary>Như <see cref="RealizedReturnOnDeployedPercent"/> nhưng bỏ phí/thuế — dùng so sánh net vs gross.</summary>
    public decimal? RealizedGrossReturnPercent { get; set; }

    /// <summary><c>Good</c>|<c>Flat</c>|<c>Failed</c> — xem <c>OutcomeBucketNames</c>.</summary>
    public string? RealizedOutcomeBucket { get; set; }

    /// <summary><c>Measured</c>|<c>Approximate</c>|<c>MissingSellPrice</c>.</summary>
    public string? RealizedStatus { get; set; }

    /// <summary><c>FeeProfile.Key()</c> tại thời điểm đo — đổi phí sẽ mismatch, trigger recompute.</summary>
    public string? RealizedFeeProfile { get; set; }

    public int? HoldingSessions { get; set; }
}

/// <summary>1 nhịp bán (Bán 1 nửa | Bán hết) của 1 vị thế — dựng lại lợi nhuận thực (realized P&amp;L).</summary>
public sealed class PositionSellLegEntity
{
    public Guid Id { get; set; }
    public Guid PositionId { get; set; }
    public string Symbol { get; set; } = "";

    /// <summary><c>BanNua</c> | <c>BanHet</c> — xem <c>MasterAlertKinds</c>.</summary>
    public string Signal { get; set; } = "";
    public DateOnly SellDate { get; set; }
    public decimal SellPrice { get; set; }
    public decimal SoldSize { get; set; }
    public decimal RemainingSizeAfter { get; set; }

    /// <summary><c>Fire</c> (giá lúc bắn noti VIP) | <c>ForwardT25</c> (backfill) | <c>OhlcvClose</c> (backfill).</summary>
    public string PriceSource { get; set; } = "";
    public DateTime FiredAtUtc { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Log feature + outcome từng lần bắn VIP BuyPoint (đo độ chính xác intraday).</summary>
public sealed class VipAlertFireEntity
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = "";
    public DateOnly SessionDate { get; set; }
    public DateTime FiredAtUtc { get; set; }
    public string Signal { get; set; } = "";
    public string? Branch { get; set; }
    public decimal FirePrice { get; set; }
    public decimal OpenPrice { get; set; }
    public decimal GainFromOpenPercent { get; set; }
    public decimal PacedVolumeRatio { get; set; }
    public decimal? MlProbAtFire { get; set; }
    public bool MlModelActive { get; set; }
    public int? BuyScore { get; set; }
    public decimal? PredictedHitPercent { get; set; }
    public string? MarketPhase { get; set; }
    public decimal? Rs5dPercent { get; set; }
    public decimal? AtrPercent { get; set; }
    public decimal? DistMa20Percent { get; set; }
    public decimal? Ma10 { get; set; }
    public decimal? Ma20 { get; set; }
    public decimal? Ma50 { get; set; }
    public bool? UptrendLong { get; set; }
    public long? ForeignNet { get; set; }
    public long? PropNet { get; set; }
    public decimal? SessionPressure { get; set; }
    public string? VsaLabel { get; set; }
    public bool FeaturesComplete { get; set; }
    public bool IntradayMeasured { get; set; }
    public decimal? IntradayReturnPercent { get; set; }
    public decimal? IntradayMfePercent { get; set; }
    public decimal? IntradayMaePercent { get; set; }
    public decimal? SessionHighSinceFire { get; set; }
    public decimal? SessionLowSinceFire { get; set; }
    public DateTime? IntradayMeasuredAtUtc { get; set; }
    public string? LlmDecision { get; set; }
    public string? LlmReason { get; set; }
    public int? LlmLatencyMs { get; set; }
    public string? LlmModel { get; set; }
    public bool LlmShadowMode { get; set; }
    /// <summary>Bối cảnh cảnh báo bán (chế độ, mốc, ngưỡng, pha) — JSON.</summary>
    public string? SellContextJson { get; set; }
}

public sealed class FalsePositiveMiningStateEntity
{
    public int Id { get; set; } = 1;
    public int FalsePositiveSetups { get; set; }
    public int GoodSetups { get; set; }
    public string ResultsJson { get; set; } = "[]";
    public DateTime UpdatedAt { get; set; }
}

public sealed class WeeklyOpportunityReviewEntity
{
    public DateOnly WeekStartDate { get; set; }
    public int TotalTracked { get; set; }
    public int MeasuredCount { get; set; }
    public int GoodCount { get; set; }
    public int FlatCount { get; set; }
    public int FailedCount { get; set; }
    public decimal SuccessRatePercent { get; set; }
    public decimal FailedRatePercent { get; set; }
    public int OpportunityCount { get; set; }
    public int BuyPoint1Count { get; set; }
    public int BuyPoint2Count { get; set; }
    public int CutLoss1Count { get; set; }
    public int CutAllCount { get; set; }
    public decimal OpportunitySuccessRate { get; set; }
    public decimal BuyPoint1SuccessRate { get; set; }
    public decimal BuyPoint2SuccessRate { get; set; }
    public string RecommendedAction { get; set; } = "Keep";
    public string Summary { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
}

public sealed class HitCalibrationBucketEntity
{
    public string BucketId { get; set; } = "";
    public int PredictedMin { get; set; }
    public int PredictedMax { get; set; }
    public int SampleCount { get; set; }
    public int GoodCount { get; set; }
    public decimal PredictedMidPercent { get; set; }
    public decimal ActualHitRatePercent { get; set; }
    public decimal CalibrationFactor { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class HitCalibrationStateEntity
{
    public int Id { get; set; } = 1;
    public decimal GlobalFactor { get; set; } = 1m;
    public int TotalSamples { get; set; }
    public decimal PredictionBiasPercent { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class ShadowPickEntity
{
    public Guid Id { get; set; }
    public DateOnly ForTradingDate { get; set; }
    public int VariantMinPassScore { get; set; }
    public string Symbol { get; set; } = "";
    public int Rank { get; set; }
    public int Score { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal PredictedHitPercent { get; set; }
    public bool OutcomeMeasured { get; set; }
    public decimal? ForwardReturnPercent { get; set; }
    public string? OutcomeBucket { get; set; }
    public DateTime? MeasuredAt { get; set; }
}

public sealed class ShadowVariantSummaryEntity
{
    public int VariantMinPassScore { get; set; }
    public int MeasuredCount { get; set; }
    public int GoodCount { get; set; }
    public int FlatCount { get; set; }
    public int FailedCount { get; set; }
    public decimal SuccessRatePercent { get; set; }
    public bool IsProduction { get; set; }
    public bool IsLeader { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ShadowWeightPickEntity
{
    public Guid Id { get; set; }
    public DateOnly ForTradingDate { get; set; }
    public decimal WeightMultiplier { get; set; }
    public string Symbol { get; set; } = "";
    public int Rank { get; set; }
    public int Score { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal PredictedHitPercent { get; set; }
    public bool OutcomeMeasured { get; set; }
    public decimal? ForwardReturnPercent { get; set; }
    public string? OutcomeBucket { get; set; }
    public DateTime? MeasuredAt { get; set; }
}

public sealed class ShadowWeightSummaryEntity
{
    public decimal WeightMultiplier { get; set; }
    public int MeasuredCount { get; set; }
    public int GoodCount { get; set; }
    public int FlatCount { get; set; }
    public int FailedCount { get; set; }
    public decimal SuccessRatePercent { get; set; }
    public bool IsProduction { get; set; }
    public bool IsLeader { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class EntryTimingStateEntity
{
    public int Id { get; set; } = 1;
    public int TopOnlyMeasured { get; set; }
    public int TopOnlyGood { get; set; }
    public int ConfirmMeasured { get; set; }
    public int ConfirmGood { get; set; }
    public bool PreferMasterConfirm { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class TradeJournalEntryEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Symbol { get; set; } = "";
    public DateOnly TradeDate { get; set; }
    public string Action { get; set; } = "";
    public decimal? SizePercent { get; set; }
    public string? EngineVerdict { get; set; }
    public string? Note { get; set; }
    public int? BuyScore { get; set; }
    public decimal? PredictedHit { get; set; }
    public string? SetupDna { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class PersonalCalibrationStateEntity
{
    public Guid UserId { get; set; }
    public decimal Factor { get; set; } = 1m;
    public int SampleCount { get; set; }
    public DateTime UpdatedAt { get; set; }
}
