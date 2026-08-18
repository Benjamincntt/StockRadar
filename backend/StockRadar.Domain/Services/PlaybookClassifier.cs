using StockRadar.Domain.Enums;

namespace StockRadar.Domain.Services;

public interface IPlaybookClassifier
{
    /// <summary>
    /// Gán playbook độc quyền theo thứ tự ưu tiên:
    /// BreakoutDarvas → PullbackMa20 → ReversalBounce → Unclassified.
    /// </summary>
    PlaybookId Classify(BuyDecisionEvaluation evaluation, bool hasReversalBounceSignal = false);
}

public sealed class PlaybookClassifier : IPlaybookClassifier
{
    public PlaybookId Classify(BuyDecisionEvaluation evaluation, bool hasReversalBounceSignal = false)
    {
        if (evaluation.HasFlatBoxBreakout || evaluation.HasBreakoutEntry)
            return PlaybookId.BreakoutDarvas;

        if (evaluation.HasMaStack
            && !evaluation.HasFlatBoxBreakout
            && !evaluation.HasBreakoutEntry
            && !evaluation.HasFlatBoxSetup)
            return PlaybookId.PullbackMa20;

        if (hasReversalBounceSignal)
            return PlaybookId.ReversalBounce;

        return PlaybookId.Unclassified;
    }
}
