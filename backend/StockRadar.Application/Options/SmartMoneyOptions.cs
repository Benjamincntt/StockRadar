using StockRadar.Domain.Services;
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

    public int MinPassScore { get; set; } = 60;

    /// <summary>Giá trong/ gần nền: % so đỉnh nền tối đa để coi là còn ở nền.</summary>
    public decimal MaxGainInBasePercent { get; set; } = 5m;

    /// <summary>Ngưỡng RS percentile (%) tối thiểu để mua khi pha Unfavorable.</summary>
    public decimal MinRsPercentileForUnfavorable { get; set; } = 80m;

    public MaStackOptions MaStack { get; set; } = new();

    public MarketPhaseOptions MarketPhase { get; set; } = new();

    public SectorWaveOptions SectorWave { get; set; } = new();

    public SmartMoneySettings ToSettings() => new(
        MinHistoryDays: MinHistoryDays,
        MinAvgDailyVolume: MinAvgDailyVolume,
        MinSessionVolume: MinSessionVolume,
        MinSessionChangePercent: MinSessionChangePercent,
        BreakoutMinVolumeRatio: BreakoutMinVolumeRatio,
        MinPassScore: MinPassScore,
        MaxGainInBasePercent: MaxGainInBasePercent,
        RequireMaStack: MaStack.Enabled,
        MinSessionsForMa50: MaStack.MinSessionsForMa50,
        MinSessionsForFullStack: MaStack.MinSessionsForFullStack,
        SectorWave: SectorWave.ToSettings(),
        MaStackFavorableMode: MaStack.FavorableMode,
        MaStackNeutralMode: MaStack.NeutralMode,
        MaStackUnfavorableMode: MaStack.UnfavorableMode,
        MinRsPercentileForUnfavorable: MinRsPercentileForUnfavorable,
        MarketPhase: MarketPhase.ToThresholds());
}

public sealed class MarketPhaseOptions
{
    public decimal FtdMinGainPercent { get; set; } = 1.2m;
    public int FtdMinRallyDay { get; set; } = 4;
    public int FtdMaxRallyDay { get; set; } = 7;
    public int Ma20SlopeLookbackSessions { get; set; } = 3;
    public int HigherLowLookbackSessions { get; set; } = 60;
    public int HigherLowPivotRadius { get; set; } = 2;
    public int RallyLookbackSessions { get; set; } = 20;

    public MarketPhaseThresholds ToThresholds() => new(
        FtdMinGainPercent,
        FtdMinRallyDay,
        FtdMaxRallyDay,
        Ma20SlopeLookbackSessions,
        HigherLowLookbackSessions,
        HigherLowPivotRadius,
        RallyLookbackSessions);
}

public sealed class MaStackOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Ít nhất bao nhiêu phiên mới áp MA50.</summary>
    public int MinSessionsForMa50 { get; set; } = 50;

    /// <summary>Đủ phiên thì yêu cầu MA20 &gt; MA50 &gt; MA100 &gt; MA200.</summary>
    public int MinSessionsForFullStack { get; set; } = 200;

    public string FavorableMode { get; set; } = "Full";
    public string NeutralMode { get; set; } = "Medium";
    public string UnfavorableMode { get; set; } = "Loose";
}

/// <summary>Ngưỡng nhận diện sóng ngành (thay cho xếp hạng ngành top N).</summary>
public sealed class SectorWaveOptions
{
    /// <summary>Số mã tối thiểu để ngành được chấm sóng.</summary>
    public int MinStocksPerSector { get; set; } = 3;

    /// <summary>Độ rộng: tỉ lệ mã tăng tối thiểu (0..1).</summary>
    public decimal MinAdvancerRatio { get; set; } = 0.60m;

    /// <summary>Lực: trung vị % thay đổi phiên của ngành.</summary>
    public decimal MinMedianChangePercent { get; set; } = 1.5m;

    /// <summary>% tăng để coi một mã là "gần trần".</summary>
    public decimal NearCeilingChangePercent { get; set; } = 4m;

    /// <summary>Lực: tỉ lệ mã gần trần tối thiểu (0..1).</summary>
    public decimal MinNearCeilingRatio { get; set; } = 0.25m;

    /// <summary>Tiền vào: tổng KL phiên / KL trung bình của ngành.</summary>
    public decimal MinVolumeRatio { get; set; } = 1.3m;

    /// <summary>Xác nhận: RS ngành so VNINDEX (5 phiên) tối thiểu.</summary>
    public decimal MinSectorRs5d { get; set; } = 0m;

    /// <summary>Gãy sóng: VolumeRatio dưới ngưỡng này tính là một phiên "cạn tiền".</summary>
    public decimal FailureMaxVolumeRatio { get; set; } = 0.5m;

    /// <summary>Gãy sóng: số phiên "cạn tiền" liên tiếp để tắt trạng thái Active.</summary>
    public int FailureConsecutiveSessions { get; set; } = 3;

    /// <summary>TTL an toàn: số phiên tối đa giữ Active nếu không phiên nào tái xác nhận.</summary>
    public int MaxActiveSessions { get; set; } = 20;

    public SectorWaveSettings ToSettings() => new(
        MinStocksPerSector: MinStocksPerSector,
        MinAdvancerRatio: MinAdvancerRatio,
        MinMedianChangePercent: MinMedianChangePercent,
        NearCeilingChangePercent: NearCeilingChangePercent,
        MinNearCeilingRatio: MinNearCeilingRatio,
        MinVolumeRatio: MinVolumeRatio,
        MinSectorRs5d: MinSectorRs5d,
        FailureMaxVolumeRatio: FailureMaxVolumeRatio,
        FailureConsecutiveSessions: FailureConsecutiveSessions,
        MaxActiveSessions: MaxActiveSessions);
}
