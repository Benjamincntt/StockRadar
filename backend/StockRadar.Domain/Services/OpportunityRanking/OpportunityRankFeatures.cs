using StockRadar.Domain.Enums;

namespace StockRadar.Domain.Services.OpportunityRanking;

/// <summary>Đặc trưng T0 cho OpportunityRanker — logistic regression T+2.5.</summary>
public static class OpportunityRankFeatures
{
    public static readonly string[] Names =
    [
        "buy_score_norm",
        "predicted_hit_norm",
        "sector_inv_rank",
        "rs5d_norm",
        "volume_ratio_norm",
        "is_actionable",
        "dna_breakout",
        "dna_shakeout",
        "market_favorable",
    ];

    public static double[] Vectorize(OpportunityRankInput input)
    {
        var dna = input.SetupDna ?? "";
        var (path, dnaPhase, sectorRank) = ParseSetupDna(dna);
        var marketFavorable = input.MarketPhase == MarketWyckoffPhase.Favorable
            || dnaPhase == MarketPhaseKind.Favorable;

        return
        [
            input.BuyScore / 100.0,
            (double)Math.Clamp(input.PredictedHitPercent, 0m, 100m) / 100.0,
            1.0 / (1.0 + Math.Max(1, sectorRank > 0 ? sectorRank : input.SectorRank)),
            Math.Clamp((double)input.RelativeStrength5d / 15.0, -1.0, 1.0),
            Math.Clamp((double)input.VolumeRatio / 3.0, 0.0, 2.0),
            input.TradeState == StockTradeState.Actionable ? 1.0 : 0.0,
            path == SetupPathKind.Breakout ? 1.0 : 0.0,
            path == SetupPathKind.Shakeout ? 1.0 : 0.0,
            marketFavorable ? 1.0 : 0.0,
        ];
    }

    public static OpportunityRankInput FromTrack(
        int? opportunityScore,
        decimal? predictedHit,
        string? setupDna,
        string? tradeState)
    {
        var (_, _, sectorRank) = ParseSetupDna(setupDna);
        Enum.TryParse<StockTradeState>(tradeState, ignoreCase: true, out var ts);

        return new OpportunityRankInput(
            opportunityScore ?? 0,
            predictedHit ?? 0m,
            sectorRank > 0 ? sectorRank : 99,
            0m,
            1m,
            string.IsNullOrWhiteSpace(tradeState) ? null : ts,
            setupDna);
    }

    public static (SetupPathKind Path, MarketPhaseKind Phase, int SectorRank) ParseSetupDna(string? dna)
    {
        if (string.IsNullOrWhiteSpace(dna))
            return (SetupPathKind.Other, MarketPhaseKind.Neutral, 99);

        var parts = dna.Split('·', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var path = parts.Length > 0 ? ClassifyPath(parts[0]) : SetupPathKind.Other;
        var phase = parts.Length > 1 ? ClassifyPhase(parts[1]) : MarketPhaseKind.Neutral;
        var sector = 99;
        if (parts.Length > 2)
        {
            var m = System.Text.RegularExpressions.Regex.Match(parts[2], @"#(\d+)");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var r))
                sector = r;
        }

        return (path, phase, sector);
    }

    private static SetupPathKind ClassifyPath(string s)
    {
        if (s.Contains("Breakout", StringComparison.OrdinalIgnoreCase))
            return SetupPathKind.Breakout;
        if (s.Contains("Shakeout", StringComparison.OrdinalIgnoreCase))
            return SetupPathKind.Shakeout;
        return SetupPathKind.Other;
    }

    private static MarketPhaseKind ClassifyPhase(string s)
    {
        if (s.Contains("thuận", StringComparison.OrdinalIgnoreCase))
            return MarketPhaseKind.Favorable;
        if (s.Contains("bất lợi", StringComparison.OrdinalIgnoreCase))
            return MarketPhaseKind.Unfavorable;
        return MarketPhaseKind.Neutral;
    }

    public enum SetupPathKind { Breakout, Shakeout, Other }

    public enum MarketPhaseKind { Favorable, Neutral, Unfavorable }
}

public sealed record OpportunityRankInput(
    int BuyScore,
    decimal PredictedHitPercent,
    int SectorRank,
    decimal RelativeStrength5d,
    decimal VolumeRatio,
    StockTradeState? TradeState,
    string? SetupDna,
    MarketWyckoffPhase? MarketPhase = null)
{
    public static OpportunityRankInput FromEvaluation(
        int buyScore,
        decimal predictedHit,
        int sectorRank,
        decimal rs5d,
        decimal volumeRatio,
        StockTradeState tradeState,
        string? setupDna,
        MarketWyckoffPhase marketPhase) =>
        new(buyScore, predictedHit, sectorRank, rs5d, volumeRatio, tradeState, setupDna, marketPhase);
}
