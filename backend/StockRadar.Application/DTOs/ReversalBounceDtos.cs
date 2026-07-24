namespace StockRadar.Application.DTOs;

/// <summary>
/// Nhận định thị trường cho UI sóng hồi = cùng pha Top (<c>MarketPhaseClassifier</c>).
/// <paramref name="BreadthRegime"/> giữ metrics/gate breadth nội bộ (không dùng làm nhãn "Thị trường").
/// </summary>
public sealed record MarketRegimeDto(
    DateOnly TradingDate,
    string Regime,
    string RegimeLabel,
    bool AllowsCounterTrendEntry,
    int UniverseCount,
    decimal PctAboveMa20,
    decimal PctAboveMa50,
    decimal PctNewLow20,
    decimal PctUp,
    decimal PctDown,
    int FloorCount,
    int CeilingCount,
    decimal MedianReturnPercent,
    decimal VnIndexDrawdownPercent,
    decimal VnIndexDistanceToMa20Percent,
    bool VnIndexAboveMa20,
    bool VnIndexReclaimedMa20,
    int ImproveStreak,
    string? StatusMessage,
    string? BreadthRegime = null,
    string? BreadthRegimeLabel = null);

/// <summary>Danh sách ứng viên sóng hồi theo phiên (có phân trang).</summary>
public sealed record ReversalBounceListDto(
    IReadOnlyList<ReversalBounceItemDto> Items,
    int Page,
    int PageSize,
    int Total,
    DateOnly TradingDate,
    string? MarketRegime,
    string? StatusMessage);

public sealed record ReversalBounceItemDto(
    string Symbol,
    string Stage,
    bool IsActionable,
    decimal TotalScore,
    int RecoveryAttemptCount,
    DateOnly? CapitulationDate,
    ReversalBounceComponentScoreDto ComponentScores,
    decimal? EntryReference,
    decimal? MaxEntryPrice,
    decimal? InvalidationPrice,
    decimal? FirstTarget,
    decimal? RewardToRisk,
    decimal? PositionFactor,
    IReadOnlyList<string> RiskWarnings,
    string MarketRegime,
    IReadOnlyList<ReversalBounceReasonDto> Reasons);

public sealed record ReversalBounceComponentScoreDto(
    decimal Capitulation,
    decimal Stabilization,
    decimal Demand,
    decimal RelativeStrength,
    decimal Liquidity,
    decimal RiskPenalty);

public sealed record ReversalBounceDetailDto(
    ReversalBounceItemDto Current,
    IReadOnlyList<ReversalBounceHistoryItemDto> History);

public sealed record ReversalBounceHistoryItemDto(
    DateOnly TradingDate,
    string Stage,
    decimal TotalScore,
    IReadOnlyList<ReversalBounceReasonDto> Reasons);

public sealed record ReversalBounceReasonDto(
    string Code,
    string Label,
    decimal NumericValue,
    decimal? Threshold,
    bool Pass);
