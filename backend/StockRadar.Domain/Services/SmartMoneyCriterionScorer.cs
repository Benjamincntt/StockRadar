using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

/// <summary>
/// Chấm tiêu chí Top cơ hội từ BuyDecisionEngine (một nguồn điểm).
/// </summary>
public interface ISmartMoneyCriterionScorer
{
    IReadOnlyList<CriterionScore> ScoreCriteria(Stock stock, SmartMoneyMarketContext context);
}

public sealed class SmartMoneyCriterionScorer(IBuyDecisionEngine buyDecision) : ISmartMoneyCriterionScorer
{
    private static readonly IReadOnlyDictionary<string, CriterionType> ComponentMap =
        new Dictionary<string, CriterionType>(StringComparer.OrdinalIgnoreCase)
        {
            ["market"] = CriterionType.MarketPhase,
            ["sector"] = CriterionType.SectorStrength,
            ["rs"] = CriterionType.RelativeStrength5d,
            ["base"] = CriterionType.BaseSetup,
            ["breakout"] = CriterionType.BreakoutVolume,
            ["shakeout"] = CriterionType.ShakeoutRecovery,
            ["volume"] = CriterionType.VolumeSpike,
            ["wyckoff"] = CriterionType.WyckoffMarkup,
            ["trend"] = CriterionType.MaStack,
        };

    public IReadOnlyList<CriterionScore> ScoreCriteria(Stock stock, SmartMoneyMarketContext context)
    {
        var decision = buyDecision.Evaluate(stock, context);
        return decision.Breakdown
            .Where(c => ComponentMap.ContainsKey(c.Id))
            .Select(c => ToCriterionScore(ComponentMap[c.Id], c))
            .ToList();
    }

    private static CriterionScore ToCriterionScore(CriterionType type, BuyScoreComponent c)
    {
        var score = c.MaxPoints > 0 ? (int)Math.Round((decimal)c.Points / c.MaxPoints * 100m) : 0;
        var bias = c.Points switch
        {
            > 0 => PatternBias.Bullish,
            0 when type is CriterionType.WyckoffMarkup or CriterionType.MaStack => PatternBias.Neutral,
            _ => PatternBias.Bearish
        };
        return new(type, score, bias, $"{c.Detail} · +{c.Points}/{c.MaxPoints}đ");
    }
}
