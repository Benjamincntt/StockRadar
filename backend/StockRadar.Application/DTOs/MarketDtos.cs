using StockRadar.Domain.Enums;

namespace StockRadar.Application.DTOs;

public record MarketOverviewDto(
    string IndexSymbol,
    decimal IndexPrice,
    decimal IndexChangePercent,
    int MarketScore,
    MarketTrend Trend);

public record SectorDto(string Name, int Score, decimal ChangePercent);

public record OpportunityDto(
    string Symbol,
    string Name,
    int Score,
    decimal Price,
    decimal ChangePercent,
    decimal VolumeRatio,
    string Sector,
    DateTime? GeneratedAt);

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
    DateTime? AnalysisAvailableAt);

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
    IReadOnlyList<BasePricePeriodDto> Periods);

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
    BasePriceDto? BasePrice,
    IReadOnlyList<CriterionScoreDto> PatternScores,
    int PatternCompositeScore,
    int BundleCompositeScore,
    int OpportunityCompositeScore);

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
    bool IsActive);

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
    int RemoveCount);

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
    bool IsActive);

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
