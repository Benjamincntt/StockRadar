using System.Diagnostics;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Services.ReversalBounce;
using Xunit;

namespace StockRadar.Tests.ReversalBounce;

public sealed class ReversalBounceAnalyzerTests
{
    private static readonly ReversalBounceSettings Settings = new ReversalBounceOptions().ToSettings();
    private static readonly ReversalBounceAnalyzer Analyzer = new();

    private static Stock StockOf(IReadOnlyList<OhlcvBar> history) =>
        new("TST", "Test", "Sector", history, Exchange: "HOSE");

    [Fact]
    public void Stage_None_When_Steady_Uptrend()
    {
        var stock = StockOf(OhlcvFixtures.SteadyUptrend(80));
        var asOf = stock.History[^1].Date;

        var result = Analyzer.Analyze(stock, [], MarketRegime.Normal, 50m, asOf, Settings);

        Assert.Equal(ReversalBounceStage.None, result.Setup.Stage);
        Assert.Equal(0m, result.Setup.TotalScore);
    }

    [Fact]
    public void Stage_Is_Candidate_When_Capitulation_Then_Recovery()
    {
        var stock = StockOf(OhlcvFixtures.CapitulationStabilizationConfirmed());
        var asOf = stock.History[^1].Date;

        var result = Analyzer.Analyze(stock, [], MarketRegime.Normal, 70m, asOf, Settings);

        Assert.NotEqual(ReversalBounceStage.None, result.Setup.Stage);
        Assert.NotNull(result.Setup.CapitulationDate);
        Assert.True(result.Setup.TotalScore >= 0m && result.Setup.TotalScore <= 100m);
        Assert.True(result.Setup.ComponentScores.Capitulation > 0m);
    }

    [Fact]
    public void SetupId_Stable_For_Same_Capitulation_Date()
    {
        var a = ReversalBounceSetupId.Compute("VIC", new DateOnly(2026, 3, 10), "reversal-bounce@1.0.0");
        var b = ReversalBounceSetupId.Compute("VIC", new DateOnly(2026, 3, 10), "reversal-bounce@1.0.0");
        Assert.Equal(a, b);
    }

    [Fact]
    public void SetupId_Differs_When_Capitulation_Date_Changes()
    {
        var a = ReversalBounceSetupId.Compute("VIC", new DateOnly(2026, 3, 10), "reversal-bounce@1.0.0");
        var b = ReversalBounceSetupId.Compute("VIC", new DateOnly(2026, 4, 2), "reversal-bounce@1.0.0");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Atr_Loop_Is_Linear_Not_Quadratic_On_Long_History()
    {
        var stock = StockOf(OhlcvFixtures.LongHistoryForAtrLoopTest(6000));
        var asOf = stock.History[^1].Date;

        var sw = Stopwatch.StartNew();
        var result = Analyzer.Analyze(stock, [], MarketRegime.Normal, 50m, asOf, Settings);
        sw.Stop();

        Assert.NotNull(result.Setup);
        Assert.True(sw.ElapsedMilliseconds < 1500, $"Analyze quá chậm: {sw.ElapsedMilliseconds}ms");
    }

    [Theory]
    [InlineData(false, false, false, false, ReversalBounceStage.None)]
    [InlineData(true, true, false, false, ReversalBounceStage.Invalidated)]
    [InlineData(true, false, true, false, ReversalBounceStage.Confirmed)]
    [InlineData(true, false, false, true, ReversalBounceStage.Stabilizing)]
    [InlineData(true, false, false, false, ReversalBounceStage.Capitulating)]
    public void DeriveStage_TruthTable(bool capit, bool invalid, bool confirmed, bool stab, ReversalBounceStage expected)
    {
        Assert.Equal(expected, ReversalBounceAnalyzer.DeriveStage(capit, invalid, confirmed, stab));
    }

    [Fact]
    public void ComputeTotalScore_Clamps_Into_0_100()
    {
        var high = new ReversalBounceComponentScores(15m, 20m, 15m, 15m, 10m, 0m);
        Assert.Equal(75m, ReversalBounceAnalyzer.ComputeTotalScore(high));

        var negative = new ReversalBounceComponentScores(0m, 0m, 0m, 0m, 0m, -10m);
        Assert.Equal(0m, ReversalBounceAnalyzer.ComputeTotalScore(negative));
    }

    [Fact]
    public void NearestSupplyZone_Is_Above_Entry()
    {
        var history = OhlcvFixtures.SteadyUptrend(60);
        var entry = history[^1].Close;
        var zone = ReversalBounceAnalyzer.NearestSupplyZone(history, entry, 500m);
        Assert.True(zone > entry);
    }
}
