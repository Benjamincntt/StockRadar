using StockRadar.Domain.Entities;

namespace StockRadar.Domain.Services.ReversalBounce;

/// <summary>6 trục điểm counter-trend theo công thức (spec §5). Tổng chuẩn hoá 0..100.</summary>
public sealed record ReversalBounceComponentScores(
    decimal Capitulation,        // 0..15
    decimal Stabilization,       // 0..20
    decimal Demand,              // 0..15
    decimal RelativeStrength,    // 0..15
    decimal Liquidity,           // 0..10
    decimal RiskPenalty)         // -10..0
{
    public static ReversalBounceComponentScores Empty { get; } = new(0m, 0m, 0m, 0m, 0m, 0m);
}

/// <summary>Một lý do/điều kiện đo được (dùng cho UI &amp; audit).</summary>
public sealed record ReversalBounceReason(
    string Code,                 // "DRAWDOWN_FROM_PEAK", "RSI_OVERSOLD", ...
    string Label,                // Tiếng Việt cho UI
    decimal NumericValue,        // giá trị đo
    decimal? Threshold,          // ngưỡng so sánh
    bool Pass);                  // true = đạt điều kiện

/// <summary>Kết quả analyzer cho 1 mã tại 1 phiên (persistence-friendly, không giữ history).</summary>
public sealed record ReversalBounceSetup(
    string Symbol,
    DateOnly TradingDate,
    ReversalBounceStage Stage,
    Guid SetupId,                                    // deterministic (spec §4.2)
    DateOnly? CapitulationDate,
    decimal? CapitulationLow,
    decimal? CapitulationClose,
    int RecoveryAttemptCount,
    ReversalBounceComponentScores ComponentScores,
    decimal TotalScore,
    MarketRegime MarketRegime,
    string StrategyVersion,
    string AlgorithmParametersHash,
    int SchemaVersion,
    IReadOnlyList<ReversalBounceReason> Reasons);

/// <summary>Kế hoạch giao dịch sau decision engine (null nếu chưa Confirmed / fail hard gate).</summary>
public sealed record ReversalBounceTradePlan(
    decimal EntryReference,             // Close_T
    decimal MaxEntryPrice,              // EntryReference × (1 + GapAcceptance × ATR14%)
    decimal InvalidationPrice,
    decimal FirstTarget,
    decimal RewardToRisk,               // |Target-Entry| / |Entry-Invalidation|
    int TimeStopSessions,
    decimal PositionFactor,
    IReadOnlyList<string> RiskWarnings);

/// <summary>Setup + plan (plan null nếu không actionable).</summary>
public sealed record ReversalBounceSignal(
    ReversalBounceSetup Setup,
    ReversalBounceTradePlan? TradePlan);

/// <summary>
/// Feature vector in-memory dùng bởi analyzer + decision engine (không lưu DB).
/// <paramref name="History"/> là slice OHLCV đã lọc <c>Date &lt;= asOfDate</c>, sort tăng dần.
/// </summary>
public sealed record ReversalBounceFeatures(
    string Symbol,
    string Exchange,
    DateOnly AsOfDate,
    IReadOnlyList<OhlcvBar> History,
    IReadOnlyList<OhlcvBar> IndexHistory,
    decimal Ma20,
    decimal Ma20Prev,
    decimal Ma50,
    decimal Atr,
    decimal Rsi,
    decimal? PeakHigh,
    decimal? CapitulationLow,
    decimal? CapitulationClose,
    DateOnly? CapitulationDate,
    int CapitulationIndex,
    decimal DrawdownPercent,
    decimal DrawdownInAtr)
{
    public OhlcvBar Latest => History[^1];

    public decimal Close => History.Count > 0 ? History[^1].Close : 0m;

    /// <summary>ATR14 quy về % giá đóng cửa hiện tại.</summary>
    public decimal AtrPercent => Close > 0 ? Atr / Close : 0m;
}

/// <summary>Output analyzer: setup (persist) + features (in-memory, cho decision engine).</summary>
public sealed record ReversalBounceAnalysis(
    ReversalBounceSetup Setup,
    ReversalBounceFeatures Features);
