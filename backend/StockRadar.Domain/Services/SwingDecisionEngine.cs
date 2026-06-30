using StockRadar.Domain.Enums;

namespace StockRadar.Domain.Services;

public enum SwingVerdict
{
    Go,
    Wait,
    NoGo,
}

public sealed record EntryTimingHint(
    decimal TopOnlySuccessRate,
    decimal TopWithConfirmSuccessRate,
    int TopOnlySamples,
    int ConfirmSamples,
    bool PreferMasterConfirm);

public sealed record SwingDecisionInput(
    BuyDecisionEvaluation Decision,
    RegimeOverlayResult Regime,
    EntryTimingHint? EntryTiming,
    decimal RiskPercentPerTrade,
    decimal MaxPositionPercent,
    bool HadMasterConfirmToday = false);

public sealed record SwingDecisionResult(
    SwingVerdict Verdict,
    string Headline,
    string Detail,
    decimal AdjustedHitPercent,
    decimal SuggestedSizePercent,
    decimal RiskRewardRatio,
    decimal RegimeSizeFactor,
    bool RequiresMasterConfirm,
    IReadOnlyList<string> Reasons);

public static class SwingDecisionEngine
{
    public static SwingDecisionResult Evaluate(SwingDecisionInput input)
    {
        var d = input.Decision;
        var regime = input.Regime;
        var reasons = new List<string>();
        reasons.AddRange(regime.Notes);

        var requiresConfirm = input.EntryTiming?.PreferMasterConfirm == true
            && d.Entry.Type != EntryPointType.Breakout
            && d.Entry.Type != EntryPointType.Shakeout;

        if (requiresConfirm && !input.HadMasterConfirmToday)
            reasons.Add("Chờ xác nhận Mua điểm 1 (timing học từ lịch sử)");

        var rr = d.Entry.RiskRewardRatio;
        var size = ComputeSizePercent(
            d.Entry.EntryPrice,
            d.Entry.StopLoss,
            input.RiskPercentPerTrade,
            input.MaxPositionPercent,
            regime.SizeFactorPercent);

        if (!d.PassesTopFilter)
        {
            return Build(
                SwingVerdict.NoGo,
                "Không vào",
                d.GateFailure ?? "Chưa đạt Top cơ hội",
                regime.AdjustedHitPercent,
                0,
                rr,
                regime.SizeFactorPercent,
                requiresConfirm,
                reasons);
        }

        if (d.Recommendation == BuyRecommendation.Avoid || regime.AdjustedHitPercent < 42m)
        {
            reasons.Add(regime.AdjustedHitPercent < 42m
                ? $"P điều chỉnh {regime.AdjustedHitPercent:0.#}% quá thấp"
                : "Khuyến nghị tránh");
            return Build(
                SwingVerdict.NoGo,
                "Không vào",
                "Setup chưa đủ edge sau regime",
                regime.AdjustedHitPercent,
                0,
                rr,
                regime.SizeFactorPercent,
                requiresConfirm,
                reasons);
        }

        if (requiresConfirm && !input.HadMasterConfirmToday)
        {
            return Build(
                SwingVerdict.Wait,
                "Chờ xác nhận",
                "Top đạt — chờ Mua điểm 1 hoặc breakout trigger",
                regime.AdjustedHitPercent,
                Math.Round(size * 0.5m, 1),
                rr,
                regime.SizeFactorPercent,
                true,
                reasons);
        }

        if (!d.Entry.IsActionable
            || d.Recommendation == BuyRecommendation.Watch
            || d.Entry.Status == EntryPointStatus.Watch)
        {
            reasons.Add(d.Entry.Action);
            return Build(
                SwingVerdict.Wait,
                "Chờ trigger",
                d.Entry.Headline,
                regime.AdjustedHitPercent,
                Math.Round(size * 0.6m, 1),
                rr,
                regime.SizeFactorPercent,
                requiresConfirm,
                reasons);
        }

        if (d.Recommendation == BuyRecommendation.StrongBuy && regime.AdjustedHitPercent >= 50m)
        {
            reasons.Add($"P điều chỉnh {regime.AdjustedHitPercent:0.#}%");
            reasons.Add($"Size gợi ý {size:0.#}% NAV (risk {input.RiskPercentPerTrade}%)");
            return Build(
                SwingVerdict.Go,
                "Cân nhắc vào",
                d.Entry.Headline,
                regime.AdjustedHitPercent,
                size,
                rr,
                regime.SizeFactorPercent,
                requiresConfirm,
                reasons);
        }

        reasons.Add("Theo dõi — chưa đủ conviction");
        return Build(
            SwingVerdict.Wait,
            "Theo dõi",
            d.Entry.Headline,
            regime.AdjustedHitPercent,
            Math.Round(size * 0.5m, 1),
            rr,
            regime.SizeFactorPercent,
            requiresConfirm,
            reasons);
    }

    private static decimal ComputeSizePercent(
        decimal entry,
        decimal stop,
        decimal riskPercent,
        decimal maxPosition,
        decimal regimeSizeFactor)
    {
        if (entry <= 0 || stop <= 0 || stop >= entry)
            return 0;

        var stopDistancePct = (entry - stop) / entry * 100m;
        if (stopDistancePct <= 0)
            return 0;

        var raw = riskPercent / stopDistancePct * 100m * (regimeSizeFactor / 100m);
        return Math.Round(Math.Clamp(raw, 0m, maxPosition), 1);
    }

    private static SwingDecisionResult Build(
        SwingVerdict verdict,
        string headline,
        string detail,
        decimal hit,
        decimal size,
        decimal rr,
        decimal regimeSize,
        bool requiresConfirm,
        List<string> reasons) =>
        new(
            verdict,
            headline,
            detail,
            hit,
            size,
            rr,
            regimeSize,
            requiresConfirm,
            reasons);
}
