using StockRadar.Application.DTOs;
using StockRadar.Domain.Enums;

namespace StockRadar.Application.Abstractions;

public interface ICriterionScoringService
{
    Task<CriteriaSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<CriterionScoreDto> ScoreIndicatorsLive(
        IReadOnlyList<Domain.Entities.OhlcvBar> history);

    /// <summary>So sánh các bộ trọng số reliability trên dữ liệu quá khứ (train/test theo ngày).</summary>
    Task<ReliabilityBacktestDto> BacktestReliabilityWeightsAsync(
        int days = 30,
        CancellationToken cancellationToken = default);
}

public static class CriterionLabels
{
    private static readonly Dictionary<CriterionType, (string Vi, string Group, int Rank)> Map = new()
    {
        [CriterionType.Rsi] = ("RSI", "Momentum", 1),
        [CriterionType.MovingAverage] = ("EMA/SMA", "Xu hướng", 2),
        [CriterionType.Macd] = ("MACD", "Xu hướng + Momentum", 3),
        [CriterionType.Volume] = ("Volume", "Khối lượng", 4),
        [CriterionType.Vwap] = ("VWAP", "Dòng tiền TN", 5),
        [CriterionType.BollingerBands] = ("Bollinger Bands", "Biến động", 6),
        [CriterionType.Atr] = ("ATR", "Biến động", 7),
        [CriterionType.Ichimoku] = ("Ichimoku Cloud", "Xu hướng", 8),
        [CriterionType.Stochastic] = ("Stochastic", "Momentum", 9),
        [CriterionType.Adx] = ("ADX", "Sức mạnh XT", 10),

        [CriterionType.BundleBeginner] = ("Mới", "Bộ chỉ báo", 11),
        [CriterionType.BundleIntermediate] = ("Trung cấp", "Bộ chỉ báo", 12),
        [CriterionType.BundleAdvanced] = ("Nâng cao", "Bộ chỉ báo", 13),
        [CriterionType.BundleProfessional] = ("Chuyên nghiệp", "Bộ chỉ báo", 14),
        [CriterionType.BundleInstitutional] = ("Tổ chức", "Bộ chỉ báo", 15),
        [CriterionType.BundleSmartMoneyConcept] = ("Smart Money", "Bộ chỉ báo", 16),

        [CriterionType.MarketPhase] = ("Pha thị trường", "Top cơ hội", 20),
        [CriterionType.SectorStrength] = ("Sức mạnh ngành", "Top cơ hội", 21),
        [CriterionType.RelativeStrength5d] = ("RS 5 phiên", "Top cơ hội", 22),
        [CriterionType.BaseSetup] = ("Hộp tích lũy phẳng", "Top cơ hội", 23),
        [CriterionType.BreakoutVolume] = ("Breakout + volume", "Top cơ hội", 24),
        [CriterionType.ShakeoutRecovery] = ("Shakeout hồi", "Top cơ hội", 25),
        [CriterionType.VolumeSpike] = ("Volume spike", "Top cơ hội", 26),
        [CriterionType.WyckoffMarkup] = ("Wyckoff markup", "Top cơ hội", 27),
        [CriterionType.MaStack] = ("MA stack", "Top cơ hội", 28),
    };

    public static string GetVi(CriterionType type) =>
        Map.TryGetValue(type, out var m) ? m.Vi : type.ToString();

    public static string GetGroup(CriterionType type) =>
        Map.TryGetValue(type, out var m) ? m.Group : "Khác";

    public static int GetRank(CriterionType type) =>
        Map.TryGetValue(type, out var m) ? m.Rank : 99;

    public static bool IsIndicator(CriterionType type) => GetRank(type) is >= 1 and <= 10;

    public static bool IsBundle(CriterionType type) => GetRank(type) is >= 11 and <= 16;

    public static bool IsOpportunity(CriterionType type) => GetRank(type) >= 20;

    public static string GetBundleComponents(CriterionType type) => type switch
    {
        CriterionType.BundleBeginner => "EMA + RSI + Volume",
        CriterionType.BundleIntermediate => "EMA + Volume + ATR",
        CriterionType.BundleAdvanced => "VWAP + EMA + Volume + ATR",
        CriterionType.BundleProfessional => "Wyckoff + VSA",
        CriterionType.BundleInstitutional => "Volume Profile + VWAP + Delta",
        CriterionType.BundleSmartMoneyConcept => "SMC + Volume + VWAP",
        _ => "",
    };
}
