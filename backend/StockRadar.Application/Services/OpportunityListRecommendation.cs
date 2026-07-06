using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

/// <summary>
/// Khuyến nghị hiển thị trên list Cơ hội tốt nhất — legacy; dùng <see cref="TradeStateResolver"/> thay thế.
/// </summary>
[Obsolete("Use TradeStateResolver + TradeStateLabels.ToLegacyRecommendation for backward-compatible API.")]
public static class OpportunityListRecommendation
{
    public static string Resolve(bool strictPass, BuyDecisionEvaluation decision)
    {
        if (decision.Entry.Status == EntryPointStatus.Late)
            return nameof(BuyRecommendation.Avoid);

        if (strictPass)
            return ResolveStrict(decision);

        return ResolveRelaxed(decision);
    }

    /// <summary>Sửa bản ghi cũ trong DB (trước khi lưu recommendation riêng cho list).</summary>
    public static string? NormalizeStored(string? recommendation, int score)
    {
        if (!string.Equals(recommendation, nameof(BuyRecommendation.Avoid), StringComparison.Ordinal))
            return recommendation;

        return score >= 80
            ? nameof(BuyRecommendation.StrongBuy)
            : nameof(BuyRecommendation.Watch);
    }

    private static string ResolveStrict(BuyDecisionEvaluation decision)
    {
        if (decision.Recommendation == BuyRecommendation.StrongBuy)
            return nameof(BuyRecommendation.StrongBuy);

        if (decision.Recommendation == BuyRecommendation.Watch)
            return nameof(BuyRecommendation.Watch);

        // Pass Top filter nhưng engine trả Avoid (vd. score 60–69 + Ready) → vẫn là theo dõi
        return nameof(BuyRecommendation.Watch);
    }

    private static string ResolveRelaxed(BuyDecisionEvaluation decision)
    {
        if (decision.BuyScore >= 80 && decision.Entry.Status == EntryPointStatus.Ready)
            return nameof(BuyRecommendation.StrongBuy);

        return nameof(BuyRecommendation.Watch);
    }
}
