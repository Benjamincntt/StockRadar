namespace StockRadar.Infrastructure.Persistence.Entities;

public sealed class SetupTrackEntity
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = "";
    public string SourceType { get; set; } = "";
    public DateOnly EntryDate { get; set; }
    public decimal EntryPrice { get; set; }
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
