using StockRadar.Domain.Enums;

namespace StockRadar.Application.DTOs;

public record MarketOverviewDto(
    string IndexSymbol,
    decimal IndexPrice,
    decimal IndexChangePercent,
    int MarketScore,
    MarketTrend Trend);

public record SectorDto(string Name, int Score, decimal ChangePercent);

public record EntryPointCheckDto(string Id, string Label, bool Passed, string Detail);

public record BuyScoreComponentDto(string Id, string Label, int Points, int MaxPoints, string Detail);

public record BuyDecisionDto(
    int BuyScore,
    int ActionScore,
    string Recommendation,
    bool PassesTopFilter,
    string? GateFailure,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<BuyScoreComponentDto> Breakdown,
    EntryPointDto EntryPoint,
    string TradeState,
    string TradeStateLabelVi,
    string TradeStateReason,
    decimal PredictedHitPercent = 0,
    int PredictedSampleCount = 0,
    string? SetupDna = null,
    IReadOnlyList<string>? TopExplainLines = null,
    SwingDecisionDto? SwingDecision = null);

public record SwingDecisionDto(
    string Verdict,
    string Headline,
    string Detail,
    decimal AdjustedHitPercent,
    decimal RawHitPercent,
    decimal SuggestedSizePercent,
    decimal RiskRewardRatio,
    decimal RegimeSizeFactor,
    bool RequiresMasterConfirm,
    IReadOnlyList<string> RegimeNotes,
    IReadOnlyList<string> Reasons,
    decimal PersonalCalibrationFactor,
    decimal? WinRate7d,
    int MeasuredCount7d);

public record CreateTradeJournalRequest(
    string Symbol,
    string Action,
    DateOnly? TradeDate = null,
    decimal? SizePercent = null,
    string? EngineVerdict = null,
    string? Note = null,
    int? BuyScore = null,
    decimal? PredictedHit = null,
    string? SetupDna = null);

public record TradeJournalEntryDto(
    Guid Id,
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

public record PersonalCalibrationDto(
    decimal Factor,
    int SampleCount,
    DateTime UpdatedAt);

public record EntryPointDto(
    string Status,
    string Type,
    int Confidence,
    decimal EntryPrice,
    decimal StopLoss,
    decimal TriggerPrice,
    decimal TargetPrice,
    decimal BaseLow,
    decimal BaseHigh,
    decimal GainFromBasePercent,
    decimal RiskRewardRatio,
    bool IsActionable,
    string Headline,
    string Action,
    IReadOnlyList<EntryPointCheckDto> Checklist);

public record OpportunityDto(
    string Symbol,
    string Name,
    int Score,
    decimal Price,
    decimal ChangePercent,
    decimal VolumeRatio,
    string Sector,
    DateTime? GeneratedAt,
    EntryPointDto? EntryPoint = null,
    string? Recommendation = null,
    string? TradeState = null,
    string? TradeStateLabelVi = null,
    string? TradeStateReason = null,
    decimal PredictedHitPercent = 0,
    int PredictedSampleCount = 0,
    string? SetupDna = null,
    IReadOnlyList<string>? TopExplainLines = null);

public record OpportunitiesListDto(
    IReadOnlyList<OpportunityDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    bool HasFreshData,
    string? StatusMessage,
    DateOnly? ForTradingDate,
    DateTime? GeneratedAt,
    bool NeedsAnalysis,
    bool CanRunAnalysis,
    DateTime? AnalysisAvailableAt,
    EngineTrustDto? EngineTrust = null,
    /// <summary>not_run | zero_matches | has_results | reference_list</summary>
    string? AnalysisStatus = null,
    DateTime? LastAnalysisAt = null,
    DateOnly? TargetTradingDate = null,
    int? LastAnalysisStocksScored = null,
    int? LastAnalysisOpportunitiesSaved = null);

public record EarlyRecoveryItemDto(
    string Symbol,
    string Name,
    string Sector,
    decimal Price,
    decimal ChangePercent,
    decimal VolumeRatio,
    decimal Rs5,
    decimal RsPercentile,
    string MarketPhase,
    string Reason,
    DateTime GeneratedAt);

public record EarlyRecoveryListDto(
    IReadOnlyList<EarlyRecoveryItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    DateOnly? ForTradingDate,
    DateTime? GeneratedAt,
    string? StatusMessage = null);

public record ShadowVariantStatusDto(
    int MinPassScore,
    int MeasuredCount,
    decimal SuccessRatePercent,
    bool IsProduction,
    bool IsLeader);

public record EngineTrustDto(
    decimal? WinRate7d,
    int MeasuredCount7d,
    int GoodCount7d,
    decimal CalibrationGlobalFactor,
    int CalibrationSamples,
    DateOnly? DataAsOfDate,
    bool ShadowModeEnabled,
    int? ShadowLeaderMinPassScore,
    string? ShadowStatusMessage,
    IReadOnlyList<ShadowVariantStatusDto>? ShadowVariants);

public record SignalDto(
    string Symbol,
    SignalType Type,
    string Title,
    string Description,
    DateTime CreatedAt);

public record ScoreBreakdownDto(
    int MarketTrend,
    int SectorStrength,
    int RelativeStrength,
    int Accumulation,
    int Breakout,
    int VolumeExpansion);

public record OhlcvBarDto(
    string Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);

public record ChartBarDto(
    string Time,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);

public record StockChartDto(
    string Symbol,
    string Interval,
    IReadOnlyList<ChartBarDto> Bars);

public record FlatBoxDto(
    decimal BoxLow,
    decimal BoxHigh,
    int SessionDays,
    string RefBoxPeriod,
    bool IsBreakoutConfirmed,
    decimal? PriceGainPercent,
    decimal? VolumeMultiplier,
    decimal SuggestedStopLoss,
    decimal GainFromBoxTopPercent,
    bool ExceedsRunupFilter,
    decimal FilterBoxTop,
    decimal FilterGainFromBoxTopPercent,
    string EventLabel,
    IReadOnlyList<BasePricePeriodDto> Periods);

public record BasePricePeriodDto(
    string FromDate,
    string ToDate,
    int SessionDays,
    decimal Low,
    decimal High);

public record BasePriceDto(
    decimal BaseLow,
    decimal BaseHigh,
    int TotalSessionDays,
    decimal GainFromBasePercent,
    int BaseIndex,
    int TotalBases,
    decimal FilterBaseHigh,
    decimal FilterGainFromBasePercent,
    bool ExceedsRunupFilter,
    int QualityScore,
    BaseQualityComponentsDto? Quality,
    IReadOnlyList<BasePricePeriodDto> Periods);

public record BaseQualityComponentsDto(
    int PriorTrendScore,
    int AtrContractionScore,
    int CompressionScore,
    int VolumeDryScore,
    int ContractionPatternScore,
    int DistributionScore,
    int DurationScore,
    int TotalScore);

/// <summary>
/// Chi tiết mã. <see cref="Score"/> = Buy Score:
/// nếu có snapshot Top cơ hội (phiên mục tiêu / mới nhất) → dùng snapshot;
/// không thì tính on-the-fly. <see cref="BuyScoreSource"/> = snapshot|live.
/// </summary>
public record StockDetailDto(
    string Symbol,
    string Name,
    string Sector,
    decimal Price,
    decimal ChangePercent,
    int Score,
    int SectorRank,
    bool PassesSmartMoneyFilter,
    IReadOnlyList<string> ScoreReasons,
    string Summary,
    IReadOnlyList<string> ActiveSignals,
    decimal BuyZone,
    decimal StopLoss,
    decimal Resistance,
    decimal Target,
    decimal RelativeStrength,
    decimal VolumeRatio,
    IReadOnlyList<OhlcvBarDto> History,
    FlatBoxDto? FlatBox,
    IReadOnlyList<CriterionScoreDto> PatternScores,
    int PatternCompositeScore,
    int BundleCompositeScore,
    int OpportunityCompositeScore,
    EntryPointDto EntryPoint,
    BuyDecisionDto BuyDecision,
    DateTime? BuyScoreAsOf = null,
    string BuyScoreSource = "live");

public record CriterionScoreDto(
    string Id,
    string Label,
    string Group,
    int Rank,
    int Score,
    string Bias,
    string Summary);

public record CriterionAccuracyDto(
    string Id,
    string Label,
    string Group,
    int Rank,
    int HitCount,
    int TotalCount,
    decimal AccuracyPercent,
    decimal AvgScore,
    decimal Weight,
    decimal Accuracy7d,
    decimal Accuracy30d,
    string RecommendedAction,
    bool IsActive,
    decimal ReliabilityScore = 0,
    decimal EdgePercent = 0,
    decimal AvgMfePercent = 0,
    decimal InvalidationRatePercent = 0,
    decimal BaselinePercent = 0,
    IReadOnlyList<CriterionBucketDto>? Buckets = null,
    IReadOnlyList<CriterionPhaseDto>? Phases = null,
    IReadOnlyList<CriterionHorizonDto>? Horizons = null);

public record CriterionBucketDto(string BucketId, int HitCount, int TotalCount, decimal AccuracyPercent);

/// <summary>Accuracy của tiêu chí ở khung đo bổ sung (T+10, T+20).</summary>
public record CriterionHorizonDto(
    int Horizon,
    int HitCount,
    int TotalCount,
    decimal AccuracyPercent,
    decimal EdgePercent,
    decimal AvgMfePercent);

public record ReliabilityBacktestDto(
    int RequestedDays,
    int TrainDays,
    int TestDays,
    int CriteriaCount,
    IReadOnlyList<ReliabilityWeightCandidateDto> Candidates,
    string? BestCandidate,
    string Note);

public record ReliabilityWeightCandidateDto(
    string Name,
    decimal HitWeight,
    decimal EdgeWeight,
    decimal MfeWeight,
    decimal BaseIntactWeight,
    decimal RankCorrelation);

public record CriterionPhaseDto(string Phase, int HitCount, int TotalCount, decimal AccuracyPercent);

public record CriterionGroupAccuracyDto(
    string GroupId,
    int HitCount,
    int TotalCount,
    decimal AccuracyPercent,
    decimal AvgScore,
    int CriterionCount,
    string RecommendedAction,
    int KeepCount,
    int WatchCount,
    int RemoveCount,
    decimal ReliabilityScore = 0,
    decimal EdgePercent = 0);

public record WeeklyCriterionReviewDto(
    string Id,
    string Label,
    string Group,
    int Rank,
    int HitCount7d,
    int TotalCount7d,
    decimal Accuracy7d,
    decimal AvgScore7d,
    decimal Weight,
    string RecommendedAction,
    bool IsActive,
    decimal Reliability7d = 0,
    decimal Edge7d = 0,
    decimal AvgMfe7d = 0,
    decimal InvalidationRate7d = 0,
    IReadOnlyList<CriterionBucketDto>? Buckets = null,
    IReadOnlyList<CriterionPhaseDto>? Phases = null);

public record CriteriaSummaryDto(
    DateOnly? AsOfDate,
    DateOnly? WeekStartDate,
    DateTime? GeneratedAt,
    IReadOnlyList<CriterionAccuracyDto> Criteria,
    IReadOnlyList<CriterionGroupAccuracyDto> Groups,
    IReadOnlyList<WeeklyCriterionReviewDto> WeeklyReview,
    IReadOnlyList<CriterionStockRankDto> TopStocks,
    string? StatusMessage);

public record CriterionStockRankDto(
    string Symbol,
    int CompositeScore,
    IReadOnlyList<CriterionScoreDto> TopCriteria);

public record RadarItemDto(
    string Symbol,
    string Name,
    string Sector,
    int Score,
    decimal Price,
    decimal ChangePercent,
    decimal VolumeRatio,
    decimal RelativeStrength,
    IReadOnlyList<SignalType> Signals);

public record AlertDto(
    Guid Id,
    string Symbol,
    SignalType Type,
    string Title,
    string Message,
    DateTime CreatedAt,
    AlertCategory Category,
    decimal? VolumeRatio,
    decimal? RelativeStrength,
    string? SectorRank,
    bool InOpportunity = false,
    bool InWatchlist = false);

public record WatchlistItemDto(
    string Symbol,
    string Name,
    string Sector,
    int Score,
    decimal ChangePercent,
    bool SectorLocked);

public record SectorCatalogItemDto(string Name);

public record UpdateStockSectorRequest(string Sector);

public record StockSectorUpdateResultDto(string Symbol, string Sector, bool SectorLocked);

public record CreateWatchlistItemRequest(string Symbol);

public record StockSearchHitDto(string Symbol, string Name);

public class RadarQuery : Common.PaginationQuery
{
    public bool Breakout { get; set; }
    public bool Accumulation { get; set; }
    public bool RelativeStrength { get; set; }
    public bool VolumeSpike { get; set; }
    public bool Shakeout { get; set; }
    public bool Distribution { get; set; }
    public string? Sector { get; set; }
}

public class AlertQuery : Common.PaginationQuery
{
    public AlertCategory Category { get; set; } = AlertCategory.All;
    public SignalType? Type { get; set; }
    public AlertFeedScope Feed { get; set; } = AlertFeedScope.Opportunity;
}

public enum AlertFeedScope
{
    Opportunity,
    Universe
}

public record TradeEventDto(
    string Symbol,
    string Label,
    decimal Price,
    long Volume,
    decimal ValueVnd,
    decimal SpreadPct,
    long BookImbalance,
    long ForeignNetDelta,
    long SessionForeignNet,
    long SessionPropNet,
    decimal SessionPressure,
    DateTime At,
    bool IsAggregated);

[Obsolete("Use TradeEventDto")]
public record TradePrintDto(
    string Symbol,
    string Side,
    decimal Price,
    long Volume,
    DateTime At);

public record SessionFlowDto(
    string Symbol,
    long SessionForeignNet,
    long SessionPropNet,
    long LastBookImbalance,
    decimal SessionPressure,
    DateTime UpdatedAt);

public record FlowLeaderDto(
    string Symbol,
    long SessionForeignNet,
    long SessionPropNet,
    decimal SessionPressure,
    int Rank);

public record IntradayMonitorStatusDto(
    bool Enabled,
    bool MarketOpen,
    int IntervalSeconds,
    DateTime? LastScanAt,
    int LastSymbolsScanned,
    int LastAlertsSent,
    string Status,
    bool IsStale);
