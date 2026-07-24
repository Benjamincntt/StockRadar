using StockRadar.Domain.Services.ReversalBounce;

namespace StockRadar.Application.Options;

/// <summary>
/// Cấu hình chiến lược counter-trend ReversalBounce.
/// - 0B: <see cref="Regime"/> (breadth regime thresholds) + <see cref="ToRegimeThresholds"/>.
/// - 0C: analyzer/decision params + <see cref="ToSettings"/> → Domain <see cref="ReversalBounceSettings"/>.
/// </summary>
public sealed class ReversalBounceOptions
{
    public const string SectionName = "ReversalBounce";

    /// <summary>Version tag cho breadth snapshot 0B (giữ ổn định, tách khỏi analyzer version).</summary>
    public const string BreadthVersion = "reversal-bounce-breadth@0.1.0";

    /// <summary>Version chiến lược analyzer (đưa vào snapshot PK). 0C nâng lên 1.0.0.</summary>
    public string StrategyVersion { get; set; } = "reversal-bounce@1.0.0";

    public int SchemaVersion { get; set; } = 1;

    /// <summary>Bật/tắt breadth+regime (0B) và analyzer counter-trend (0C) trong daily pipeline.</summary>
    public bool Enabled { get; set; } = true;

    // ── 0B: breadth regime thresholds ──────────────────────────────────────
    public RegimeThresholdOptions Regime { get; set; } = new();

    // ── 0C: Capitulation ───────────────────────────────────────────────────
    public decimal MinDrawdownPercent { get; set; } = 18m;
    public decimal MinDrawdownInAtr { get; set; } = 2.5m;
    public decimal OversoldRsiThreshold { get; set; } = 25m;
    public decimal SellingClimaxVolMultiple { get; set; } = 2.5m;
    public int WideDownBarsMinCount { get; set; } = 3;
    public int WideDownBarsWindow { get; set; } = 10;
    public decimal WideDownBarsRangeToAtr { get; set; } = 1.2m;

    // ── 0C: Stabilization ──────────────────────────────────────────────────
    public decimal StabilizationNoNewLowToleranceAtr { get; set; } = 1m;
    public decimal RangeContractionRatio { get; set; } = 0.7m;
    public int StabilizationMinSessions { get; set; } = 2;
    public decimal LowerWickRatioThreshold { get; set; } = 0.55m;
    public int LowerWickMinCount { get; set; } = 2;

    // ── 0C: Confirmation ───────────────────────────────────────────────────
    public int ConfirmationLookbackHigh { get; set; } = 2;
    public decimal StrongCloseClvThreshold { get; set; } = 0.65m;
    public decimal DemandExpansionVolMultiple { get; set; } = 1.4m;
    public decimal GapCancelAtrMultiple { get; set; } = 0.5m;
    public decimal GapAcceptanceAtrMultiple { get; set; } = 0.15m;
    public int ConfirmationEmaShort { get; set; } = 5;
    public int ConfirmationEmaLong { get; set; } = 10;

    // ── 0C: Invalidation ───────────────────────────────────────────────────
    public decimal InvalidConfirmationBufferAtr { get; set; } = 1m;

    // ── 0C: Windows ────────────────────────────────────────────────────────
    public int LookbackSessions { get; set; } = 80;
    public int MaShortWindow { get; set; } = 20;
    public int MaLongWindow { get; set; } = 50;
    public int AtrWindow { get; set; } = 14;
    public int RsiWindow { get; set; } = 14;

    // ── 0C: Universe filter ────────────────────────────────────────────────
    public int MinHistoryDays { get; set; } = 60;
    public decimal MinAvgDailyVolume { get; set; } = 100_000m;

    // ── 0C: Hard gate per regime + trade ───────────────────────────────────
    public ReversalBounceRegimeGateOptions RegimeThresholds { get; set; } = new();
    public ReversalBounceTradeOptions Trade { get; set; } = new();

    public MarketRegimeThresholds ToRegimeThresholds() => new(
        PanicMaxDrawdownPercent: Regime.PanicMaxDrawdownPercent,
        PanicMaxPctAboveMa20: Regime.PanicMaxPctAboveMa20,
        PanicMinFloorCount: Regime.PanicMinFloorCount,
        PanicExitImproveStreak: Regime.PanicExitImproveStreak,
        ReboundMinPctAboveMa20: Regime.ReboundMinPctAboveMa20,
        NormalMinPctAboveMa20: Regime.NormalMinPctAboveMa20);

    public ReversalBounceSettings ToSettings() => new(
        StrategyVersion: StrategyVersion,
        SchemaVersion: SchemaVersion,
        MinDrawdownPercent: MinDrawdownPercent,
        MinDrawdownInAtr: MinDrawdownInAtr,
        OversoldRsiThreshold: OversoldRsiThreshold,
        SellingClimaxVolMultiple: SellingClimaxVolMultiple,
        WideDownBarsMinCount: WideDownBarsMinCount,
        WideDownBarsWindow: WideDownBarsWindow,
        WideDownBarsRangeToAtr: WideDownBarsRangeToAtr,
        StabilizationNoNewLowToleranceAtr: StabilizationNoNewLowToleranceAtr,
        RangeContractionRatio: RangeContractionRatio,
        StabilizationMinSessions: StabilizationMinSessions,
        LowerWickRatioThreshold: LowerWickRatioThreshold,
        LowerWickMinCount: LowerWickMinCount,
        ConfirmationLookbackHigh: ConfirmationLookbackHigh,
        StrongCloseClvThreshold: StrongCloseClvThreshold,
        DemandExpansionVolMultiple: DemandExpansionVolMultiple,
        GapCancelAtrMultiple: GapCancelAtrMultiple,
        GapAcceptanceAtrMultiple: GapAcceptanceAtrMultiple,
        ConfirmationEmaShort: ConfirmationEmaShort,
        ConfirmationEmaLong: ConfirmationEmaLong,
        InvalidConfirmationBufferAtr: InvalidConfirmationBufferAtr,
        LookbackSessions: LookbackSessions,
        MaShortWindow: MaShortWindow,
        MaLongWindow: MaLongWindow,
        AtrWindow: AtrWindow,
        RsiWindow: RsiWindow,
        MinHistoryDays: MinHistoryDays,
        MinAvgDailyVolume: MinAvgDailyVolume,
        RegimeGate: RegimeThresholds.ToGate(),
        Trade: Trade.ToSettings());
}

public sealed class RegimeThresholdOptions
{
    /// <summary>Vào Panic khi VN-Index drawdown ≤ giá trị này (âm).</summary>
    public decimal PanicMaxDrawdownPercent { get; set; } = -8m;

    /// <summary>Vào Panic khi % mã trên MA20 ≤ giá trị này.</summary>
    public decimal PanicMaxPctAboveMa20 { get; set; } = 20m;

    /// <summary>Vào Panic khi số mã đóng cửa (gần) sàn ≥ giá trị này.</summary>
    public int PanicMinFloorCount { get; set; } = 50;

    /// <summary>Số phiên cải thiện liên tiếp cần có để thoát Panic (nâng chậm).</summary>
    public int PanicExitImproveStreak { get; set; } = 2;

    /// <summary>Vào ReboundConfirmed khi VN-Index lấy lại MA20 và % mã trên MA20 ≥ giá trị này.</summary>
    public decimal ReboundMinPctAboveMa20 { get; set; } = 50m;

    /// <summary>Ngưỡng % mã trên MA20 để coi thị trường "khỏe" (Normal).</summary>
    public decimal NormalMinPctAboveMa20 { get; set; } = 45m;
}

public sealed class ReversalBounceRegimeGateOptions
{
    public decimal StabilizingMinScore { get; set; } = 80m;
    /// <summary>Phải ≤ max DemandScore (15). Spec cũ 18/20 lệch scale MVP.</summary>
    public decimal StabilizingMinDemand { get; set; } = 13m;
    public decimal ReboundConfirmedMinScore { get; set; } = 72m;
    public decimal ReboundConfirmedMinDemand { get; set; } = 12m;
    public decimal NormalMinScore { get; set; } = 75m;
    public decimal NormalMinDemand { get; set; } = 14m;
    public decimal MinLiquidityScore { get; set; } = 5m;
    public decimal MaxRiskPenalty { get; set; } = -5m;
    public decimal StabilizingPositionFactor { get; set; } = 0.25m;
    public decimal ReboundConfirmedPositionFactor { get; set; } = 0.50m;
    public decimal NormalPositionFactor { get; set; } = 0.40m;

    public ReversalBounceRegimeGate ToGate() => new(
        StabilizingMinScore: StabilizingMinScore,
        StabilizingMinDemand: StabilizingMinDemand,
        ReboundConfirmedMinScore: ReboundConfirmedMinScore,
        ReboundConfirmedMinDemand: ReboundConfirmedMinDemand,
        NormalMinScore: NormalMinScore,
        NormalMinDemand: NormalMinDemand,
        MinLiquidityScore: MinLiquidityScore,
        MaxRiskPenalty: MaxRiskPenalty,
        StabilizingPositionFactor: StabilizingPositionFactor,
        ReboundConfirmedPositionFactor: ReboundConfirmedPositionFactor,
        NormalPositionFactor: NormalPositionFactor);
}

public sealed class ReversalBounceTradeOptions
{
    public int TimeStopSessions { get; set; } = 10;
    public int MaxHoldSessions { get; set; } = 20;
    public decimal MinRewardToRisk { get; set; } = 1.5m;
    public decimal SlippageBaseBps { get; set; } = 10m;
    public decimal SlippageGapImpactCoeff { get; set; } = 0.5m;
    public decimal SlippageFloorLockPenaltyBps { get; set; } = 30m;
    public decimal FeeBuyPercent { get; set; } = 0.15m;
    public decimal FeeSellPercent { get; set; } = 0.15m;
    public decimal TaxSellPercent { get; set; } = 0.10m;
    public int MinTradingSessionsToSell { get; set; } = 3;
    public int MaxSignalsPerDay { get; set; } = 5;

    public ReversalBounceTradeSettings ToSettings() => new(
        TimeStopSessions: TimeStopSessions,
        MaxHoldSessions: MaxHoldSessions,
        MinRewardToRisk: MinRewardToRisk,
        MinTradingSessionsToSell: MinTradingSessionsToSell,
        MaxSignalsPerDay: MaxSignalsPerDay,
        SlippageBaseBps: SlippageBaseBps,
        SlippageGapImpactCoeff: SlippageGapImpactCoeff,
        SlippageFloorLockPenaltyBps: SlippageFloorLockPenaltyBps,
        FeeBuyPercent: FeeBuyPercent,
        FeeSellPercent: FeeSellPercent,
        TaxSellPercent: TaxSellPercent);
}
