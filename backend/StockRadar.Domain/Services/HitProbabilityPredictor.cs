using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

public static class HitProbabilityPredictor
{
    public sealed record HitForecast(
        decimal PredictedHitPercent,
        int SampleCount,
        string SetupDna,
        IReadOnlyList<string> TopExplainLines);

    public static HitForecast Predict(
        int buyScore,
        IReadOnlyList<BuyScoreComponent> breakdown,
        EntryPointEvaluation entry,
        SmartMoneyMarketContext context,
        int sectorRank)
    {
        var profile = context.Adaptive;
        var active = breakdown.Where(c => c.Points > 0).ToList();
        if (active.Count == 0)
        {
            return new HitForecast(50m, 0, BuildDna(entry, context, sectorRank), []);
        }

        decimal weightedRel = 0;
        decimal totalW = 0;
        var minSamples = int.MaxValue;

        foreach (var c in active)
        {
            var state = profile.GetState(c.Id, c.MaxPoints);
            var w = (decimal)c.Points / Math.Max(1, c.MaxPoints);
            var rel = state.ReliabilityPercent > 0 ? state.ReliabilityPercent : 50m;
            weightedRel += w * rel;
            totalW += w;
            if (state.SampleCount > 0)
                minSamples = Math.Min(minSamples, state.SampleCount);
        }

        var baseProb = totalW > 0 ? weightedRel / totalW : 50m;
        var phaseMult = context.MarketPhase switch
        {
            MarketWyckoffPhase.Favorable => 1.05m,
            MarketWyckoffPhase.Neutral => 1m,
            _ => 0.9m,
        };

        var pathMult = entry.Type switch
        {
            EntryPointType.Breakout => ReliabilityFactor(profile, "breakout", 22),
            EntryPointType.Shakeout => ReliabilityFactor(profile, "shakeout", 10),
            _ => 0.98m,
        };

        var scoreMult = buyScore switch
        {
            >= 80 => 1.06m,
            >= 70 => 1.03m,
            _ => 1m,
        };

        var sectorMult = sectorRank <= 3 ? 1.04m : sectorRank <= context.Settings.TopSectorCount ? 1.01m : 0.97m;

        var predicted = Math.Clamp(baseProb * phaseMult * pathMult * scoreMult * sectorMult, 8m, 92m);
        predicted = Math.Round(predicted, 1);
        predicted = context.Calibration.Apply(predicted);

        var explain = active
            .Select(c =>
            {
                var state = profile.GetState(c.Id, c.MaxPoints);
                var rel = state.ReliabilityPercent > 0 ? state.ReliabilityPercent : 50m;
                var contrib = (decimal)c.Points / Math.Max(1, c.MaxPoints) * rel;
                return (c.Label, c.Detail, contrib);
            })
            .OrderByDescending(x => x.contrib)
            .Take(3)
            .Select(x => $"{x.Label}: {x.Detail}")
            .ToList();

        var samples = minSamples == int.MaxValue ? 0 : minSamples;
        return new HitForecast(predicted, samples, BuildDna(entry, context, sectorRank), explain);
    }

    private static decimal ReliabilityFactor(AdaptiveScoringProfile profile, string id, int baseMax)
    {
        var state = profile.GetState(id, baseMax);
        var rel = state.ReliabilityPercent > 0 ? state.ReliabilityPercent : 50m;
        return Math.Clamp(0.85m + rel / 500m, 0.85m, 1.15m);
    }

    private static string BuildDna(
        EntryPointEvaluation entry,
        SmartMoneyMarketContext context,
        int sectorRank)
    {
        var path = entry.Type switch
        {
            EntryPointType.Breakout => "Breakout",
            EntryPointType.Shakeout => "Shakeout",
            _ => "Chờ kích hoạt",
        };
        var phase = context.MarketPhase switch
        {
            MarketWyckoffPhase.Favorable => "TT thuận",
            MarketWyckoffPhase.Neutral => "TT trung tính",
            _ => "TT bất lợi",
        };
        return $"{path} · {phase} · Ngành #{sectorRank}";
    }
}
