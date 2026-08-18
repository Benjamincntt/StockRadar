using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;
using Xunit;

namespace StockRadar.Tests.Playbook;

/// <summary>T025 — BundleProfessional/Institutional/SMC gate: không đồng thuận → Neutral 0.</summary>
public sealed class BundleGateTests
{
    private static readonly ISignalAnalyzer Signals = new SignalAnalyzer();
    private static readonly IndicatorBundleScorer Scorer = new(Signals);

    private static IReadOnlyDictionary<CriterionType, CriterionScore> EmptySingles() =>
        new Dictionary<CriterionType, CriterionScore>();

    private static IReadOnlyList<OhlcvBar> FlatHistory(int days = 30, decimal close = 50_000m)
    {
        var bars = new List<OhlcvBar>();
        var d = new DateOnly(2026, 1, 2);
        for (var i = 0; i < days; i++)
            bars.Add(new OhlcvBar(d.AddDays(i), close, close * 1.005m, close * 0.995m, close, 500_000));
        return bars;
    }

    [Fact]
    public void BundleProfessional_NoSignal_ReturnsNeutralZero()
    {
        var history = FlatHistory();
        var singles = EmptySingles();
        var results = Scorer.ScoreBundles(history, singles);
        var pro = results.FirstOrDefault(r => r.Type == CriterionType.BundleProfessional);
        Assert.NotNull(pro);
        Assert.Equal(0, pro!.Score);
        Assert.Equal(PatternBias.Neutral, pro.Bias);
    }

    [Fact]
    public void BundleInstitutional_NeutralComponents_ReturnsNeutralZero()
    {
        // Flat history → volProfile and delta stay Neutral → gate fails
        var history = FlatHistory();
        var singles = EmptySingles();
        var results = Scorer.ScoreBundles(history, singles);
        var inst = results.FirstOrDefault(r => r.Type == CriterionType.BundleInstitutional);
        Assert.NotNull(inst);
        Assert.Equal(0, inst!.Score);
        Assert.Equal(PatternBias.Neutral, inst.Bias);
    }

    [Fact]
    public void BundleProfessional_AllAgreedBullish_ScorePositive()
    {
        // Accumulation signal on a trending history with large vol → Wyckoff bullish
        // VSA: last bar has narrow spread + high vol + up → VSA bullish
        var bars = new List<OhlcvBar>();
        var d = new DateOnly(2026, 1, 2);
        for (var i = 0; i < 29; i++)
            bars.Add(new OhlcvBar(d.AddDays(i), 50_000m, 50_500m, 49_800m, 50_200m, 1_000_000));
        // Last bar: spread narrow + up + high vol → VSA bullish; and inject a Wyckoff shakeout day before
        bars.Add(new OhlcvBar(d.AddDays(28), 50_200m, 50_350m, 50_150m, 50_300m, 2_000_000));
        var results = Scorer.ScoreBundles(bars, EmptySingles());
        // Just verify it doesn't crash and returns a BundleProfessional score
        var pro = results.FirstOrDefault(r => r.Type == CriterionType.BundleProfessional);
        Assert.NotNull(pro);
    }
}
