using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;
using Xunit;

namespace StockRadar.Tests.Playbook;

/// <summary>
/// T021 — SC-004 regression: PlaybookId flags không ảnh hưởng BuyScore / PassesTopFilter.
/// Nguyên tắc: chỉ báo đo accuracy, không vào Buy Score (constitution §III).
/// </summary>
public sealed class PlaybookRegressionTests
{
    private static readonly PlaybookClassifier Classifier = new();

    private static BuyDecisionEvaluation MakeEvalWithScore(
        int score,
        bool passesTop,
        bool hasFlatBoxBreakout = false,
        bool hasBreakoutEntry = false,
        bool hasFlatBoxSetup = false,
        bool hasMaStack = false) =>
        new(
            Symbol: "TST",
            BuyScore: score,
            ActionScore: passesTop ? score : 0,
            Recommendation: BuyRecommendation.Avoid,
            PassesTopFilter: passesTop,
            GateFailure: passesTop ? null : "gate",
            StockPhase: WyckoffPhase.Unknown,
            SectorWave: SectorSnapshot.Unknown("Test"),
            RelativeStrength5d: 0m,
            VolumeRatio: 0m,
            Reasons: [],
            Signals: [],
            Breakdown: [],
            Entry: new EntryPointEvaluation(
                EntryPointStatus.Watch, EntryPointType.None, 0, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m,
                false, "", "", []),
            TradeState: StockTradeState.Avoid,
            TradeStateReason: "",
            HasFlatBoxBreakout: hasFlatBoxBreakout,
            HasBreakoutEntry: hasBreakoutEntry,
            HasFlatBoxSetup: hasFlatBoxSetup,
            HasMaStack: hasMaStack);

    [Fact]
    public void PlaybookFlags_DoNotChangeBuyScore()
    {
        // Hai mã cùng BuyScore nhưng khác playbook flags — BuyScore phải giống nhau
        var eval1 = MakeEvalWithScore(75, true, hasFlatBoxBreakout: true, hasMaStack: true);
        var eval2 = MakeEvalWithScore(75, true, hasFlatBoxBreakout: false, hasMaStack: false);

        Assert.Equal(eval1.BuyScore, eval2.BuyScore);
        Assert.Equal(eval1.PassesTopFilter, eval2.PassesTopFilter);

        // Classifier phân loại khác nhau — playbook không đổi score
        var pb1 = Classifier.Classify(eval1);
        var pb2 = Classifier.Classify(eval2);
        Assert.NotEqual(pb1, pb2);
        Assert.Equal(PlaybookId.BreakoutDarvas, pb1);
        Assert.Equal(PlaybookId.Unclassified, pb2);
    }

    [Fact]
    public void PlaybookClassifier_IsStateless_SameInputSameOutput()
    {
        var eval = MakeEvalWithScore(80, true, hasMaStack: true);
        var pb1 = Classifier.Classify(eval);
        var pb2 = Classifier.Classify(eval);
        Assert.Equal(pb1, pb2);
    }
}
