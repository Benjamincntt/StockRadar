namespace StockRadar.Domain.Enums;

public enum MarketTrend
{
    Uptrend,
    Sideway,
    Downtrend
}

public enum SignalType
{
    Breakout,
    DarvasBreakout,
    VolumeSpike,
    Accumulation,
    Shakeout,
    Distribution,
    RelativeStrength
}

public enum AlertCategory
{
    Buy,
    Sell,
    All
}
