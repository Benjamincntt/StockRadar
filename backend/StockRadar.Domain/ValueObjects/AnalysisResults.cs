using StockRadar.Domain.Enums;

namespace StockRadar.Domain.ValueObjects;

public sealed record CriterionAccuracySettings(
    int ForwardSessions = 2,
    int MinScoreForEvaluation = 60,
    decimal DirectionThresholdPercent = 3m,
    decimal SwingTargetPercent = 3m,
    bool RequireTrendSetup = false,
    bool RequireRelativeStrength = true,
    bool RequireBaseIntact = true,
    decimal ReliabilityHitWeight = 0.4m,
    decimal ReliabilityEdgeWeight = 0.3m,
    decimal ReliabilityMfeWeight = 0.2m,
    decimal ReliabilityBaseIntactWeight = 0.1m);

public sealed record ScoreBreakdown(
    int MarketTrend,
    int SectorStrength,
    int RelativeStrength,
    int Accumulation,
    int Breakout,
    int VolumeExpansion)
{
    public int Total =>
        MarketTrend + SectorStrength + RelativeStrength + Accumulation + Breakout + VolumeExpansion;
}

public sealed record PriceLevels(
    decimal BuyZone,
    decimal StopLoss,
    decimal Resistance,
    decimal Target);

public sealed record StockScore(
    int Total,
    ScoreBreakdown Breakdown,
    decimal RelativeStrength,
    decimal VolumeRatio,
    decimal ChangePercent);

public sealed record SectorScore(
    string Name,
    int Score,
    decimal ChangePercent,
    int Rank);

/// <summary>Vùng tích lũy gần nhất — biên độ high/low trong ngưỡng.</summary>
public sealed record ConsolidationZone(
    int StartIndex,
    int EndIndex,
    decimal BaseLow,
    decimal BaseHigh,
    decimal RangePercent);

public sealed record BasePriceFilterSettings(
    int ConsolidationMinSessions = 10,
    int MaxScanSessions = 90,
    int MaxBaseWindowSessions = 45,
    decimal MaxGainFromBasePercent = 10m,
    int MinBaseQualityScore = 50,
    int StrongBaseQualityScore = 80,
    int IdealBaseMinSessions = 15,
    int IdealBaseMaxSessions = 40,
    decimal MinPriorImpulsePercent = 15m,
    int PriorImpulseLookbackSessions = 30,
    DarvasBoxSettings? Darvas = null);

public sealed record SmartMoneySettings(
    int MinHistoryDays = 21,
    decimal MinAvgDailyVolume = 800_000m,
    /// <summary>KL khớp tối thiểu trong phiên kích hoạt (breakout / shakeout hồi phục).</summary>
    decimal MinSessionVolume = 800_000m,
    /// <summary>% tăng tối thiểu trong phiên kích hoạt.</summary>
    decimal MinSessionChangePercent = 3m,
    decimal BreakoutMinVolumeRatio = 1.5m,
    int MinPassScore = 60,
    decimal MaxGainInBasePercent = 5m,
    bool RequireMaStack = true,
    int MinSessionsForMa50 = 50,
    int MinSessionsForFullStack = 200,
    SectorWaveSettings? SectorWave = null,
    string MaStackFavorableMode = "Full",
    string MaStackNeutralMode = "Medium",
    string MaStackUnfavorableMode = "Loose",
    decimal MinRsPercentileForUnfavorable = 80m,
    MarketPhaseThresholds? MarketPhase = null)
{
    public MarketPhaseThresholds PhaseThresholds => MarketPhase ?? MarketPhaseThresholds.Default;

    public SectorWaveSettings SectorWaveThresholds => SectorWave ?? SectorWaveSettings.Default;
}

/// <summary>
/// Ngưỡng nhận diện "sóng ngành" trong phiên — thay cho xếp hạng ngành top N.
/// </summary>
public sealed record SectorWaveSettings(
    /// <summary>Số mã tối thiểu để một ngành được chấm sóng.</summary>
    int MinStocksPerSector = 3,
    /// <summary>Độ rộng: tỉ lệ mã tăng tối thiểu (0..1).</summary>
    decimal MinAdvancerRatio = 0.60m,
    /// <summary>Lực: trung vị % thay đổi phiên của ngành.</summary>
    decimal MinMedianChangePercent = 1.5m,
    /// <summary>% tăng để coi một mã là "gần trần".</summary>
    decimal NearCeilingChangePercent = 4m,
    /// <summary>Lực: tỉ lệ mã gần trần tối thiểu (0..1).</summary>
    decimal MinNearCeilingRatio = 0.25m,
    /// <summary>Tiền vào: tổng KL phiên / KL trung bình.</summary>
    decimal MinVolumeRatio = 1.3m,
    /// <summary>Xác nhận: RS ngành so VNINDEX (5 phiên) tối thiểu.</summary>
    decimal MinSectorRs5d = 0m,
    /// <summary>Gãy sóng: VolumeRatio dưới ngưỡng này tính là một phiên "cạn tiền".</summary>
    decimal FailureMaxVolumeRatio = 0.5m,
    /// <summary>Gãy sóng: số phiên "cạn tiền" liên tiếp để tắt trạng thái Active.</summary>
    int FailureConsecutiveSessions = 3,
    /// <summary>TTL an toàn: số phiên tối đa giữ Active nếu không phiên nào tái xác nhận.</summary>
    int MaxActiveSessions = 20)
{
    public static SectorWaveSettings Default { get; } = new();
}

/// <summary>
/// Trạng thái "sóng ngành" xuyên phiên (Sector Wave Regime) — khác <see cref="SectorSnapshot.Wave"/>
/// (chỉ phản ánh đúng phiên hiện tại). Kích hoạt khi <see cref="SectorSnapshot.HasWave"/> đúng,
/// giữ Active qua các phiên "nghỉ" cho tới khi đủ <see cref="SectorWaveSettings.FailureConsecutiveSessions"/>
/// phiên liên tiếp cạn volume, hoặc hết hạn <see cref="SectorWaveSettings.MaxActiveSessions"/>.
/// </summary>
public sealed record SectorWaveRegimeState(
    string Sector,
    DateOnly TradingDate,
    bool IsActive,
    DateOnly ActivatedOn,
    int SessionsSinceActivation,
    int ConsecutiveLowVolumeSessions,
    DateOnly? FailedOn);

/// <summary>
/// Ảnh chụp một ngành trong phiên: độ rộng tăng/giảm, lực, tiền vào, RS — và trạng thái sóng.
/// </summary>
public sealed record SectorSnapshot(
    string Name,
    int StockCount,
    int Advancers,
    int Decliners,
    decimal AdvancerRatio,
    decimal MedianChangePercent,
    decimal NearCeilingRatio,
    decimal VolumeRatio,
    decimal AvgRs5d,
    decimal AvgChange5d,
    decimal TotalAvgVolume,
    SectorWaveState Wave)
{
    /// <summary>Ngành không đủ mã / không phân loại được — coi như không có sóng.</summary>
    public static SectorSnapshot Unknown(string name) =>
        new(name, 0, 0, 0, 0m, 0m, 0m, 0m, 0m, 0m, 0m, SectorWaveState.None);

    public bool HasWave => Wave != SectorWaveState.None;

    /// <summary>Thứ hạng sóng dùng cho feature ML: 1 = sóng mạnh, 2 = chớm sóng, 3 = không sóng.</summary>
    public int WaveRank => Wave switch
    {
        SectorWaveState.Strong => 1,
        SectorWaveState.Emerging => 2,
        _ => 3
    };

    /// <summary>Giá trị hiển thị cho người dùng: "12 tăng / 1 giảm".</summary>
    public string BreadthDetail => StockCount > 0
        ? $"{Advancers} tăng / {Decliners} giảm"
        : "Không đủ mã trong ngành";

    public string WaveLabel => Wave switch
    {
        SectorWaveState.Strong => "Sóng ngành mạnh",
        SectorWaveState.Emerging => "Chớm sóng ngành",
        _ => "Chưa có sóng ngành"
    };

    /// <summary>Điểm sóng 0–100 để xếp danh sách ngành (không phải xếp hạng dùng cho Buy Score).</summary>
    public int WaveScore
    {
        get
        {
            if (StockCount == 0)
                return 0;

            var breadth = Math.Clamp(AdvancerRatio, 0m, 1m) * 40m;
            var strength = Math.Clamp(MedianChangePercent / 3m, 0m, 1m) * 25m;
            var ceiling = Math.Clamp(NearCeilingRatio * 2m, 0m, 1m) * 20m;
            var money = Math.Clamp((VolumeRatio - 1m) / 0.8m, 0m, 1m) * 15m;
            return (int)Math.Round(Math.Clamp(breadth + strength + ceiling + money, 0m, 100m));
        }
    }
}

public sealed record BasePricePeriod(
    DateOnly FromDate,
    DateOnly ToDate,
    int SessionDays,
    decimal Low,
    decimal High);

public sealed record BasePriceProfile(
    decimal BaseLow,
    decimal BaseHigh,
    int TotalSessionDays,
    IReadOnlyList<BasePricePeriod> Periods,
    decimal GainFromBasePercent,
    int BaseIndex = 1,
    int TotalBases = 1,
    int QualityScore = 0,
    BaseQualityComponents? Quality = null);
