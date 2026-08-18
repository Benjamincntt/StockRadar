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
    RelativeStrength,
    BullishDivergence
}

/// <summary>Trạng thái sóng ngành trong phiên (thay cho xếp hạng ngành top N).</summary>
public enum SectorWaveState
{
    /// <summary>Không có sóng.</summary>
    None,
    /// <summary>Chớm sóng — đủ độ rộng nhưng chưa đủ lực/tiền/RS.</summary>
    Emerging,
    /// <summary>Sóng mạnh — đủ độ rộng + lực + tiền vào + khỏe hơn VNINDEX.</summary>
    Strong
}

public enum AlertCategory
{
    Buy,
    Sell,
    All
}
