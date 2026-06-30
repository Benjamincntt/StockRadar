using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Options;

public sealed class SmartMoneyOptions
{
    public const string SectionName = "SmartMoney";

    public int MinHistoryDays { get; set; } = 21;

    /// <summary>Thanh khoản TB tối thiểu (cp/phiên).</summary>
    public decimal MinAvgDailyVolume { get; set; } = 800_000m;

    /// <summary>KL khớp tối thiểu trong phiên breakout / shakeout hồi phục.</summary>
    public decimal MinSessionVolume { get; set; } = 800_000m;

    /// <summary>% tăng tối thiểu trong phiên kích hoạt xu hướng.</summary>
    public decimal MinSessionChangePercent { get; set; } = 3m;

    public decimal BreakoutMinVolumeRatio { get; set; } = 1.5m;

    public int TopSectorCount { get; set; } = 5;

    public int MinPassScore { get; set; } = 60;

    /// <summary>Giá trong/ gần nền: % so đỉnh nền tối đa để coi là còn ở nền.</summary>
    public decimal MaxGainInBasePercent { get; set; } = 5m;

    public MaStackOptions MaStack { get; set; } = new();

    public SectorRankWeightsOptions SectorRankWeights { get; set; } = new();

    public SmartMoneySettings ToSettings() => new(
        MinHistoryDays,
        MinAvgDailyVolume,
        MinSessionVolume,
        MinSessionChangePercent,
        BreakoutMinVolumeRatio,
        TopSectorCount,
        MinPassScore,
        MaxGainInBasePercent,
        MaStack.Enabled,
        MaStack.MinSessionsForMa50,
        MaStack.MinSessionsForFullStack,
        SectorRankWeights.RelativeStrength,
        SectorRankWeights.TotalVolume,
        SectorRankWeights.CapProxy,
        SectorRankWeights.StockCount);
}

public sealed class MaStackOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Ít nhất bao nhiêu phiên mới áp MA50.</summary>
    public int MinSessionsForMa50 { get; set; } = 50;

    /// <summary>Đủ phiên thì yêu cầu MA20 &gt; MA50 &gt; MA100 &gt; MA200.</summary>
    public int MinSessionsForFullStack { get; set; } = 200;
}

public sealed class SectorRankWeightsOptions
{
    public double RelativeStrength { get; set; } = 0.35;
    public double TotalVolume { get; set; } = 0.25;
    public double CapProxy { get; set; } = 0.25;
    public double StockCount { get; set; } = 0.15;
}
