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
