using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using Xunit;

namespace StockRadar.Tests.Playbook;

public sealed class PlaybookClassifierTests
{
    private static readonly PlaybookClassifier Classifier = new();

    private static BuyDecisionEvaluation MakeEval(
        bool hasFlatBoxBreakout = false,
        bool hasBreakoutEntry = false,
        bool hasFlatBoxSetup = false,
        bool hasMaStack = false) =>
        new(
            Symbol: "TEST",
            BuyScore: 0,
            ActionScore: 0,
            Recommendation: BuyRecommendation.Avoid,
            PassesTopFilter: false,
            GateFailure: null,
            StockPhase: WyckoffPhase.Unknown,
            SectorRank: 1,
            RelativeStrength5d: 0m,
            VolumeRatio: 0m,
            Reasons: [],
            Signals: [],
            Breakdown: [],
            Entry: new EntryPointEvaluation(
                EntryPointStatus.Watch,
                EntryPointType.None,
                Confidence: 0,
                EntryPrice: 0m,
                StopLoss: 0m,
                TriggerPrice: 0m,
                TargetPrice: 0m,
                BaseLow: 0m,
                BaseHigh: 0m,
                GainFromBasePercent: 0m,
                RiskRewardRatio: 0m,
                IsActionable: false,
                Headline: "",
                Action: "",
                Checklist: []),
            TradeState: StockTradeState.Avoid,
            TradeStateReason: "",
            HasFlatBoxBreakout: hasFlatBoxBreakout,
            HasBreakoutEntry: hasBreakoutEntry,
            HasFlatBoxSetup: hasFlatBoxSetup,
            HasMaStack: hasMaStack);

    [Fact]
    public void BreakoutAndMaStack_ReturnsBreakoutDarvas()
    {
        var eval = MakeEval(hasFlatBoxBreakout: true, hasMaStack: true);
        Assert.Equal(PlaybookId.BreakoutDarvas, Classifier.Classify(eval));
    }

    [Fact]
    public void MaStackOnly_ReturnsPullbackMa20()
    {
        var eval = MakeEval(hasMaStack: true);
        Assert.Equal(PlaybookId.PullbackMa20, Classifier.Classify(eval));
    }

    [Fact]
    public void MaStackWithSetupZone_DoesNotReturnPullback()
    {
        // mã trong setup zone Darvas không rơi vào pullback-ma20
        var eval = MakeEval(hasMaStack: true, hasFlatBoxSetup: true);
        Assert.NotEqual(PlaybookId.PullbackMa20, Classifier.Classify(eval));
    }

    [Fact]
    public void NoMatch_ReturnsUnclassified()
    {
        var eval = MakeEval();
        Assert.Equal(PlaybookId.Unclassified, Classifier.Classify(eval));
    }

    [Fact]
    public void HasReversalBounceSignal_ReturnsReversalBounce()
    {
        var eval = MakeEval();
        Assert.Equal(PlaybookId.ReversalBounce, Classifier.Classify(eval, hasReversalBounceSignal: true));
    }
}
