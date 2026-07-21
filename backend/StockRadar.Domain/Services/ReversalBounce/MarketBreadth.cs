namespace StockRadar.Domain.Services.ReversalBounce;

/// <summary>
/// Snapshot độ rộng thị trường + regime cho một phiên. Bất biến, suy ra hoàn toàn từ
/// OHLCV toàn universe + VN-Index history (stateless trừ hysteresis lấy từ snapshot phiên trước).
/// </summary>
public sealed record MarketBreadthSnapshot(
    DateOnly TradingDate,
    int UniverseCount,
    decimal PctAboveMa20,
    decimal PctAboveMa50,
    decimal PctNewLow20,
    decimal PctUp,
    decimal PctDown,
    int FloorCount,
    int CeilingCount,
    decimal MedianReturnPercent,
    decimal MedianTurnover,
    decimal VnIndexDrawdownPercent,
    decimal VnIndexDistanceToMa20Percent,
    bool VnIndexAboveMa20,
    bool VnIndexReclaimedMa20,
    MarketRegime Regime,
    int ImproveStreak);

/// <summary>
/// Ngưỡng phân loại regime (seed MVP — tinh chỉnh qua shadow mode Phase 1).
/// Truyền từ <c>ReversalBounceOptions</c> (Application) xuống classifier.
/// </summary>
public sealed record MarketRegimeThresholds(
    decimal PanicMaxDrawdownPercent,
    decimal PanicMaxPctAboveMa20,
    int PanicMinFloorCount,
    int PanicExitImproveStreak,
    decimal ReboundMinPctAboveMa20,
    decimal NormalMinPctAboveMa20)
{
    /// <summary>Ngưỡng mặc định theo đề xuất owner (câu 5).</summary>
    public static MarketRegimeThresholds Default { get; } = new(
        PanicMaxDrawdownPercent: -8m,
        PanicMaxPctAboveMa20: 20m,
        PanicMinFloorCount: 50,
        PanicExitImproveStreak: 2,
        ReboundMinPctAboveMa20: 50m,
        NormalMinPctAboveMa20: 45m);
}
