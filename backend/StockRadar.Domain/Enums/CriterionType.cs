namespace StockRadar.Domain.Enums;

/// <summary>Chỉ báo kỹ thuật (top 10) + tiêu chí SmartMoney — backtest T-1 và điều chỉnh trọng số.</summary>
public enum CriterionType
{
    Rsi = 1,
    MovingAverage,
    Macd,
    Volume,
    Vwap,
    BollingerBands,
    Atr,
    Ichimoku,
    Stochastic,
    Adx,

    [Obsolete("Bundle đơn giản bị loại bỏ — dùng các bundle chuyên biệt theo playbook")]
    BundleBeginner,
    [Obsolete("Bundle đơn giản bị loại bỏ — dùng các bundle chuyên biệt theo playbook")]
    BundleIntermediate,
    [Obsolete("Bundle đơn giản bị loại bỏ — dùng các bundle chuyên biệt theo playbook")]
    BundleAdvanced,
    BundleProfessional,
    BundleInstitutional,
    BundleSmartMoneyConcept,

    MarketPhase,
    SectorStrength,
    RelativeStrength5d,
    BaseSetup,
    BreakoutVolume,
    ShakeoutRecovery,
    VolumeSpike,
    WyckoffMarkup,
    MaStack,
}

public enum PatternBias
{
    Neutral,
    Bullish,
    Bearish,
}
