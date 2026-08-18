using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;
using Xunit;

namespace StockRadar.Tests.Playbook;

/// <summary>T020 — cùng một mã cho ra outcome khác nhau khi playbook khác nhau (horizon/target khác).</summary>
public sealed class PlaybookOutcomeTests
{
    private static readonly TrendSetupEvaluator Evaluator = new(new SignalAnalyzer());

    private static IReadOnlyList<OhlcvBar> BuildHistory(int days, decimal close = 50_000m)
    {
        var bars = new List<OhlcvBar>();
        var d = new DateOnly(2026, 1, 2);
        for (var i = 0; i < days; i++)
            bars.Add(new OhlcvBar(d.AddDays(i), close, close * 1.02m, close * 0.99m, close, 1_000_000));
        return bars;
    }

    [Fact]
    public void SameStock_DifferentPlaybookHorizons_GiveDifferentForwardSessions()
    {
        var breakoutCfg = new PlaybookOutcomeConfig { ForwardSessions = 2, SwingTargetPercent = 3m };
        var pullbackCfg = new PlaybookOutcomeConfig { ForwardSessions = 5, SwingTargetPercent = 4m };

        Assert.NotEqual(breakoutCfg.ForwardSessions, pullbackCfg.ForwardSessions);
        Assert.NotEqual(breakoutCfg.SwingTargetPercent, pullbackCfg.SwingTargetPercent);
    }

    [Fact]
    public void GetPlaybookConfig_KnownPlaybook_ReturnsDedicatedConfig()
    {
        var options = new CriterionAccuracyOptions();
        var cfg = options.GetPlaybookConfig("pullback-ma20");
        Assert.Equal(5, cfg.ForwardSessions);
        Assert.Equal(4m, cfg.SwingTargetPercent);
    }

    [Fact]
    public void GetPlaybookConfig_UnknownPlaybook_FallsBackToGlobal()
    {
        var options = new CriterionAccuracyOptions { ForwardSessions = 2, SwingTargetPercent = 3m };
        var cfg = options.GetPlaybookConfig("some-unknown");
        Assert.Equal(options.ForwardSessions, cfg.ForwardSessions);
        Assert.Equal(options.SwingTargetPercent, cfg.SwingTargetPercent);
    }
}
