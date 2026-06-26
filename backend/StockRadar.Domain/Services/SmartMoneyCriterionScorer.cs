using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

/// <summary>
/// Chấm từng tiêu chí Top cơ hội theo đúng điểm cộng trong SmartMoneyOpportunitySelector.
/// </summary>
public interface ISmartMoneyCriterionScorer
{
    IReadOnlyList<CriterionScore> ScoreCriteria(Stock stock, SmartMoneyMarketContext context);
}

public sealed class SmartMoneyCriterionScorer(ISignalAnalyzer signals) : ISmartMoneyCriterionScorer
{
    private const int MaxMarketPhase = 12;
    private const int MaxSector = 18;
    private const int MaxRs5 = 20;
    private const int MaxBase = 18;
    private const int MaxBreakout = 22;
    private const int MaxShakeout = 10;
    private const int MaxVolumeSpike = 8;
    private const int MaxWyckoff = 5;
    private const int MaxMaStack = 5;

    public IReadOnlyList<CriterionScore> ScoreCriteria(Stock stock, SmartMoneyMarketContext context)
    {
        var settings = context.Settings;
        var runup = context.RunupFilter;
        var history = stock.History;
        var detected = signals.DetectSignals(stock, context.Index.ChangePercent);
        var volRatio = signals.GetVolumeRatio(history);
        var rs5 = signals.GetRelativeStrength(stock, context.IndexChangePercent5d, 5);
        var sectorRank = context.SectorRank.GetValueOrDefault(stock.Sector, context.SectorCount + 1);
        var stockPhase = ClassifyStockPhase(detected, volRatio, settings.BreakoutMinVolumeRatio);
        var hasBase = signals.HasValidBaseSetup(history, runup, settings.MaxGainInBasePercent);
        var hasBreakoutVol = detected.Contains(SignalType.Breakout) && volRatio >= settings.BreakoutMinVolumeRatio;
        var hasShakeout = detected.Contains(SignalType.Shakeout);
        var hasVolSpike = detected.Contains(SignalType.VolumeSpike);
        var hasMa = signals.HasBullishMaStack(
            history, settings.RequireMaStack, settings.MinSessionsForMa50, settings.MinSessionsForFullStack);

        return
        [
            ScoreMarketPhase(context.MarketPhase),
            ScoreSector(sectorRank, settings.TopSectorCount),
            ScoreRs5(rs5),
            ScoreBase(hasBase),
            ScoreBreakout(hasBreakoutVol, volRatio),
            ScoreShakeout(hasShakeout),
            ScoreVolumeSpike(hasVolSpike, volRatio),
            ScoreWyckoff(stockPhase),
            ScoreMaStack(hasMa),
        ];
    }

    private static CriterionScore ScoreMarketPhase(MarketWyckoffPhase phase)
    {
        var (pts, summary, bias) = phase switch
        {
            MarketWyckoffPhase.Favorable => (12, "Pha thị trường thuận", PatternBias.Bullish),
            MarketWyckoffPhase.Neutral => (6, "Thị trường trung tính", PatternBias.Neutral),
            _ => (0, "Pha thị trường bất lợi", PatternBias.Bearish),
        };
        return ToScore(CriterionType.MarketPhase, pts, MaxMarketPhase, bias, summary);
    }

    private static CriterionScore ScoreSector(int rank, int topSectorCount)
    {
        int pts;
        string summary;
        PatternBias bias;
        if (rank <= 3)
        {
            pts = 18;
            summary = $"Ngành top #{rank}";
            bias = PatternBias.Bullish;
        }
        else if (rank <= topSectorCount)
        {
            pts = 10;
            summary = $"Ngành mạnh #{rank}";
            bias = PatternBias.Bullish;
        }
        else
        {
            pts = 0;
            summary = $"Ngành yếu #{rank}";
            bias = PatternBias.Bearish;
        }
        return ToScore(CriterionType.SectorStrength, pts, MaxSector, bias, summary);
    }

    private static CriterionScore ScoreRs5(decimal rs5)
    {
        int pts;
        string summary;
        PatternBias bias;
        if (rs5 >= 3m)
        {
            pts = 20;
            summary = $"RS +{rs5:0.#}% vs VN (5 phiên)";
            bias = PatternBias.Bullish;
        }
        else if (rs5 >= 0m)
        {
            pts = 12;
            summary = "Khỏe hơn VNINDEX (5 phiên)";
            bias = PatternBias.Bullish;
        }
        else
        {
            pts = 0;
            summary = $"Yếu hơn VNINDEX RS {rs5:0.#}%";
            bias = PatternBias.Bearish;
        }
        return ToScore(CriterionType.RelativeStrength5d, pts, MaxRs5, bias, summary);
    }

    private static CriterionScore ScoreBase(bool hasBase) =>
        hasBase
            ? ToScore(CriterionType.BaseSetup, 18, MaxBase, PatternBias.Bullish, "Nền giá / tích lũy")
            : ToScore(CriterionType.BaseSetup, 0, MaxBase, PatternBias.Neutral, "Chưa có nền giá hợp lệ");

    private static CriterionScore ScoreBreakout(bool has, decimal volRatio) =>
        has
            ? ToScore(CriterionType.BreakoutVolume, 22, MaxBreakout, PatternBias.Bullish,
                $"Breakout Vol×{volRatio:0.0}")
            : ToScore(CriterionType.BreakoutVolume, 0, MaxBreakout, PatternBias.Neutral, "Chưa breakout + volume");

    private static CriterionScore ScoreShakeout(bool has) =>
        has
            ? ToScore(CriterionType.ShakeoutRecovery, 10, MaxShakeout, PatternBias.Bullish, "Đáy trước thị trường")
            : ToScore(CriterionType.ShakeoutRecovery, 0, MaxShakeout, PatternBias.Neutral, "Không có shakeout");

    private static CriterionScore ScoreVolumeSpike(bool has, decimal volRatio) =>
        has
            ? ToScore(CriterionType.VolumeSpike, 8, MaxVolumeSpike, PatternBias.Bullish, $"KL bất thường {volRatio:0.#}×")
            : ToScore(CriterionType.VolumeSpike, 0, MaxVolumeSpike, PatternBias.Neutral, "Volume bình thường");

    private static CriterionScore ScoreWyckoff(WyckoffPhase phase)
    {
        if (phase == WyckoffPhase.Markup)
            return ToScore(CriterionType.WyckoffMarkup, 5, MaxWyckoff, PatternBias.Bullish, "Pha tăng giá (markup)");
        if (phase == WyckoffPhase.Distribution)
            return ToScore(CriterionType.WyckoffMarkup, 0, MaxWyckoff, PatternBias.Bearish, "Pha phân phối — không cộng điểm");
        if (phase == WyckoffPhase.Accumulation)
            return ToScore(CriterionType.WyckoffMarkup, 0, MaxWyckoff, PatternBias.Bullish, "Pha tích lũy (chưa markup)");
        return ToScore(CriterionType.WyckoffMarkup, 0, MaxWyckoff, PatternBias.Neutral, "Pha chưa rõ");
    }

    private static CriterionScore ScoreMaStack(bool has) =>
        has
            ? ToScore(CriterionType.MaStack, 5, MaxMaStack, PatternBias.Bullish, "MA stack tăng")
            : ToScore(CriterionType.MaStack, 0, MaxMaStack, PatternBias.Bearish, "Chưa MA stack");

    private static CriterionScore ToScore(
        CriterionType type,
        int points,
        int maxPoints,
        PatternBias bias,
        string summary)
    {
        var score = maxPoints > 0 ? (int)Math.Round((decimal)points / maxPoints * 100m) : 0;
        return new(type, score, bias, $"{summary} · +{points}/{maxPoints}đ");
    }

    private static WyckoffPhase ClassifyStockPhase(
        IReadOnlyList<SignalType> detected,
        decimal volRatio,
        decimal breakoutMinVol)
    {
        if (detected.Contains(SignalType.Distribution)) return WyckoffPhase.Distribution;
        if (detected.Contains(SignalType.Breakout) && volRatio >= breakoutMinVol) return WyckoffPhase.Markup;
        if (detected.Contains(SignalType.Accumulation) || detected.Contains(SignalType.Shakeout))
            return WyckoffPhase.Accumulation;
        return WyckoffPhase.Unknown;
    }
}
