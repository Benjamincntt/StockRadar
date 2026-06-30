namespace StockRadar.Application.Options;

public sealed class SwingTradingOptions
{
    public const string SectionName = "SwingTrading";

    /// <summary>% NAV risk mỗi lệnh swing (gợi ý size).</summary>
    public decimal RiskPercentPerTrade { get; set; } = 1m;

    /// <summary>Size tối đa % NAV gợi ý.</summary>
    public decimal MaxPositionPercent { get; set; } = 25m;

    /// <summary>Win 7d dưới ngưỡng → giảm size.</summary>
    public decimal LowWinRateThreshold { get; set; } = 45m;

    /// <summary>Phiên swing đo T+5 / T+10.</summary>
    public int SwingMeasureSessionsShort { get; set; } = 5;

    public int SwingMeasureSessionsLong { get; set; } = 10;

    /// <summary>Decay tuần cũ khi tính personal calibration (0–1).</summary>
    public decimal JournalDecayFactor { get; set; } = 0.92m;
}
