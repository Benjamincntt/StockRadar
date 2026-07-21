namespace StockRadar.Domain.Services.ReversalBounce;

/// <summary>
/// Tham số analyzer/decision counter-trend đã "đóng băng" cho Domain (không phụ thuộc Application).
/// Application map từ <c>ReversalBounceOptions.ToSettings()</c>.
/// </summary>
public sealed record ReversalBounceSettings(
    string StrategyVersion,
    int SchemaVersion,
    // Capitulation
    decimal MinDrawdownPercent,
    decimal MinDrawdownInAtr,
    decimal OversoldRsiThreshold,
    decimal SellingClimaxVolMultiple,
    int WideDownBarsMinCount,
    int WideDownBarsWindow,
    decimal WideDownBarsRangeToAtr,
    // Stabilization
    decimal StabilizationNoNewLowToleranceAtr,
    decimal RangeContractionRatio,
    int StabilizationMinSessions,
    decimal LowerWickRatioThreshold,
    int LowerWickMinCount,
    // Confirmation
    int ConfirmationLookbackHigh,
    decimal StrongCloseClvThreshold,
    decimal DemandExpansionVolMultiple,
    decimal GapCancelAtrMultiple,
    decimal GapAcceptanceAtrMultiple,
    int ConfirmationEmaShort,
    int ConfirmationEmaLong,
    // Invalidation
    decimal InvalidConfirmationBufferAtr,
    // Windows
    int LookbackSessions,
    int MaShortWindow,
    int MaLongWindow,
    int AtrWindow,
    int RsiWindow,
    // Universe
    int MinHistoryDays,
    decimal MinAvgDailyVolume,
    // Hard gate + trade
    ReversalBounceRegimeGate RegimeGate,
    ReversalBounceTradeSettings Trade);

/// <summary>Ngưỡng hard-gate theo regime (spec §7.1).</summary>
public sealed record ReversalBounceRegimeGate(
    decimal StabilizingMinScore,
    decimal StabilizingMinDemand,
    decimal ReboundConfirmedMinScore,
    decimal ReboundConfirmedMinDemand,
    decimal NormalMinScore,
    decimal NormalMinDemand,
    decimal MinLiquidityScore,
    decimal MaxRiskPenalty,
    decimal StabilizingPositionFactor,
    decimal ReboundConfirmedPositionFactor,
    decimal NormalPositionFactor);

/// <summary>Tham số dựng trade plan + fill/backtest (spec §3.1 Trade, §11.2).</summary>
public sealed record ReversalBounceTradeSettings(
    int TimeStopSessions,
    int MaxHoldSessions,
    decimal MinRewardToRisk,
    int MinTradingSessionsToSell,
    int MaxSignalsPerDay,
    decimal SlippageBaseBps,
    decimal SlippageGapImpactCoeff,
    decimal SlippageFloorLockPenaltyBps,
    decimal FeeBuyPercent,
    decimal FeeSellPercent,
    decimal TaxSellPercent);
