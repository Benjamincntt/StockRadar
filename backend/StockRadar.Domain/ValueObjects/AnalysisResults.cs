namespace StockRadar.Domain.ValueObjects;

public sealed record CriterionAccuracySettings(
    int ForwardSessions = 5,
    int MinScoreForEvaluation = 60,
    decimal DirectionThresholdPercent = 3m,
    decimal SwingTargetPercent = 3m,
    bool RequireTrendSetup = true,
    bool RequireRelativeStrength = true,
    bool RequireBaseIntact = true);

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
    int ConsolidationMinSessions = 12,
    int MaxScanSessions = 60,
    int MaxBaseWindowSessions = 45,
    decimal MaxGainFromBasePercent = 10m,
    int MinBaseQualityScore = 60,
    int StrongBaseQualityScore = 80,
    int IdealBaseMinSessions = 15,
    int IdealBaseMaxSessions = 40,
    decimal MinPriorImpulsePercent = 15m,
    int PriorImpulseLookbackSessions = 30);

public sealed record SmartMoneySettings(
    int MinHistoryDays = 21,
    decimal MinAvgDailyVolume = 800_000m,
    /// <summary>KL khớp tối thiểu trong phiên kích hoạt (breakout / shakeout hồi phục).</summary>
    decimal MinSessionVolume = 800_000m,
    /// <summary>% tăng tối thiểu trong phiên kích hoạt.</summary>
    decimal MinSessionChangePercent = 3m,
    decimal BreakoutMinVolumeRatio = 1.5m,
    int TopSectorCount = 5,
    int MinPassScore = 60,
    decimal MaxGainInBasePercent = 5m,
    bool RequireMaStack = true,
    int MinSessionsForMa50 = 50,
    int MinSessionsForFullStack = 200,
    double SectorWeightRs = 0.35,
    double SectorWeightVolume = 0.25,
    double SectorWeightCap = 0.25,
    double SectorWeightCount = 0.15);

public sealed record SectorSnapshot(
    string Name,
    int Rank,
    int StockCount,
    decimal AvgChange5d,
    decimal TotalAvgVolume,
    decimal CapProxy,
    double CompositeScore);

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
