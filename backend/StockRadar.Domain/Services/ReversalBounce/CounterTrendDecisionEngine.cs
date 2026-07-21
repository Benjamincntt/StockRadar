using StockRadar.Domain.MarketData;

namespace StockRadar.Domain.Services.ReversalBounce;

public interface ICounterTrendDecisionEngine
{
    /// <summary>
    /// Áp hard gate theo regime + dựng trade plan. Trả signal với <c>TradePlan = null</c> nếu
    /// chưa Confirmed, regime Panic, hoặc fail bất kỳ gate/RR nào.
    /// </summary>
    ReversalBounceSignal Decide(
        ReversalBounceSetup setup,
        ReversalBounceFeatures features,
        ReversalBounceSettings settings);
}

public sealed class CounterTrendDecisionEngine : ICounterTrendDecisionEngine
{
    public ReversalBounceSignal Decide(
        ReversalBounceSetup setup,
        ReversalBounceFeatures features,
        ReversalBounceSettings settings)
    {
        var noPlan = new ReversalBounceSignal(setup, null);

        if (setup.Stage != ReversalBounceStage.Confirmed)
            return noPlan;
        if (setup.MarketRegime == MarketRegime.Panic)
            return noPlan;

        var gate = settings.RegimeGate;
        var (minScore, minDemand, positionFactor) = setup.MarketRegime switch
        {
            MarketRegime.Stabilizing => (gate.StabilizingMinScore, gate.StabilizingMinDemand, gate.StabilizingPositionFactor),
            MarketRegime.ReboundConfirmed => (gate.ReboundConfirmedMinScore, gate.ReboundConfirmedMinDemand, gate.ReboundConfirmedPositionFactor),
            _ => (gate.NormalMinScore, gate.NormalMinDemand, gate.NormalPositionFactor)
        };

        var s = setup.ComponentScores;
        if (setup.TotalScore < minScore) return noPlan;
        if (s.Demand < minDemand) return noPlan;
        if (s.Liquidity < gate.MinLiquidityScore) return noPlan;
        if (s.RiskPenalty < gate.MaxRiskPenalty) return noPlan;

        var entryRef = features.Close;
        if (entryRef <= 0m)
            return noPlan;

        var maxEntry = entryRef * (1m + settings.GapAcceptanceAtrMultiple * features.AtrPercent);
        var invalidation = ComputeInvalidationPrice(features, settings);
        var target = ReversalBounceAnalyzer.NearestSupplyZone(features.History, entryRef, features.Atr);

        var rAbs = Math.Abs(entryRef - invalidation);
        var rr = rAbs > 0m ? Math.Abs(target - entryRef) / rAbs : 0m;
        if (rr < settings.Trade.MinRewardToRisk)
            return noPlan;

        var plan = new ReversalBounceTradePlan(
            EntryReference: entryRef,
            MaxEntryPrice: maxEntry,
            InvalidationPrice: invalidation,
            FirstTarget: target,
            RewardToRisk: Math.Round(rr, 2),
            TimeStopSessions: settings.Trade.TimeStopSessions,
            PositionFactor: positionFactor,
            RiskWarnings: BuildRiskWarnings(setup, features));

        return new ReversalBounceSignal(setup, plan);
    }

    private static decimal ComputeInvalidationPrice(ReversalBounceFeatures f, ReversalBounceSettings opt)
    {
        var tol = f.Atr * opt.StabilizationNoNewLowToleranceAtr;
        var capitLow = f.CapitulationLow ?? f.Close - 2m * f.Atr;
        return capitLow - tol;
    }

    private static IReadOnlyList<string> BuildRiskWarnings(ReversalBounceSetup setup, ReversalBounceFeatures f)
    {
        var warnings = new List<string>();
        if (setup.ComponentScores.RiskPenalty <= -3m)
            warnings.Add("Rủi ro cao: có phiên chất sàn / giảm liên tiếp gần đây.");
        if (setup.MarketRegime == MarketRegime.Stabilizing)
            warnings.Add("Thị trường mới cân bằng — vào thăm dò, tỉ trọng thấp.");
        if (f.AtrPercent >= 0.05m)
            warnings.Add("Biến động (ATR%) lớn — biên dừng lỗ rộng.");
        return warnings;
    }
}
