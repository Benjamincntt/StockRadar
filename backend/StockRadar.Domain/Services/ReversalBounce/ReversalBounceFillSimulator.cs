using StockRadar.Domain.Entities;
using StockRadar.Domain.MarketData;

namespace StockRadar.Domain.Services.ReversalBounce;

public static class ReversalBounceExitReasons
{
    public const string Stop = "Stop";
    public const string Target = "Target";
    public const string TimeStop = "TimeStop";
    public const string MaxHold = "MaxHold";
    public const string GapCancelled = "GapCancelled";
    public const string FloorLockForced = "FloorLockForced";
    public const string DefensiveExit = "DefensiveExit";
    public const string OpenEnded = "OpenEnded";
}

/// <summary>Kết quả mô phỏng 1 lệnh (thuần, không DB).</summary>
public sealed record ReversalBounceFillResult(
    bool Entered,
    DateOnly? EntryDate,
    decimal EntryPrice,
    DateOnly? ExitDate,
    decimal? ExitPrice,
    int SessionsToExit,
    string ExitReason,
    decimal ReturnPercentGross,
    decimal ReturnPercentNet,
    decimal MaxFavorablePercent,
    decimal MaxAdversePercent,
    int FloorDeferrals);

/// <summary>
/// Mô phỏng fill/exit counter-trend (spec §11.2): vào Open(T+1), gap-cancel, bán từ T+3,
/// floor-lock defer/force, slippage + phí. Thuần &amp; deterministic để unit-test.
/// </summary>
public static class ReversalBounceFillSimulator
{
    public static ReversalBounceFillResult Simulate(
        IReadOnlyList<OhlcvBar> bars,
        int signalIndex,
        string exchange,
        ReversalBounceTradePlan plan,
        decimal atrPercent,
        decimal gapCancelAtrMultiple,
        ReversalBounceTradeSettings trade,
        bool allowDefensiveEarlyExit)
    {
        var entryIdx = signalIndex + 1;
        if (signalIndex < 0 || entryIdx >= bars.Count)
            return NoEntry(ReversalBounceExitReasons.OpenEnded);

        var closeT = bars[signalIndex].Close;
        var entryBar = bars[entryIdx];
        var gap = closeT > 0 ? entryBar.Open / closeT - 1m : 0m;
        var gapPct = gap * 100m;

        if (gap > gapCancelAtrMultiple * atrPercent)
            return NoEntry(ReversalBounceExitReasons.GapCancelled);

        var entryPrevLocked = IsFloorLocked(bars, signalIndex, exchange);
        var entrySlipBps = trade.SlippageBaseBps
            + trade.SlippageGapImpactCoeff * Math.Max(0m, gapPct - 0.5m) * 100m
            + (entryPrevLocked ? trade.SlippageFloorLockPenaltyBps : 0m);
        var entryPrice = entryBar.Open * (1m + entrySlipBps / 10_000m);
        if (entryPrice <= 0m)
            return NoEntry(ReversalBounceExitReasons.OpenEnded);

        var mfe = 0m;
        var mae = 0m;
        var deferrals = 0;
        var minSell = trade.MinTradingSessionsToSell;

        for (var k = entryIdx; k < bars.Count; k++)
        {
            var bar = bars[k];
            var sessions = k - entryIdx;

            mfe = Math.Max(mfe, (bar.High - entryPrice) / entryPrice * 100m);
            mae = Math.Min(mae, (bar.Low - entryPrice) / entryPrice * 100m);

            if (allowDefensiveEarlyExit && sessions is 1 or 2 && bar.Close <= plan.InvalidationPrice)
                return Close(bars, entryBar, entryPrice, k, bar.Close, sessions,
                    ReversalBounceExitReasons.DefensiveExit, exchange, trade, mfe, mae, deferrals);

            if (sessions < minSell)
                continue;

            var reason = ResolveExitReason(bar, plan, sessions, trade);
            if (reason is null)
                continue;

            if (IsFloorLocked(bars, k, exchange))
            {
                var lockedNext = k + 1 < bars.Count && IsFloorLocked(bars, k + 1, exchange);
                if (lockedNext)
                {
                    deferrals++;
                    var forceIdx = Math.Min(k + 2, bars.Count - 1);
                    return Close(bars, entryBar, entryPrice, forceIdx, bars[forceIdx].Open,
                        forceIdx - entryIdx, ReversalBounceExitReasons.FloorLockForced, exchange, trade, mfe, mae, deferrals);
                }

                if (k + 1 < bars.Count)
                {
                    deferrals++;
                    var nextIdx = k + 1;
                    return Close(bars, entryBar, entryPrice, nextIdx, bars[nextIdx].Close,
                        nextIdx - entryIdx, reason, exchange, trade, mfe, mae, deferrals);
                }
            }

            return Close(bars, entryBar, entryPrice, k, bar.Close, sessions, reason, exchange, trade, mfe, mae, deferrals);
        }

        var lastIdx = bars.Count - 1;
        return Close(bars, entryBar, entryPrice, lastIdx, bars[lastIdx].Close, lastIdx - entryIdx,
            ReversalBounceExitReasons.OpenEnded, exchange, trade, mfe, mae, deferrals);
    }

    private static string? ResolveExitReason(
        OhlcvBar bar, ReversalBounceTradePlan plan, int sessions, ReversalBounceTradeSettings trade)
    {
        if (bar.Close <= plan.InvalidationPrice) return ReversalBounceExitReasons.Stop;
        if (bar.Close >= plan.FirstTarget) return ReversalBounceExitReasons.Target;
        if (sessions >= trade.TimeStopSessions) return ReversalBounceExitReasons.TimeStop;
        if (sessions >= trade.MaxHoldSessions) return ReversalBounceExitReasons.MaxHold;
        return null;
    }

    private static ReversalBounceFillResult Close(
        IReadOnlyList<OhlcvBar> bars,
        OhlcvBar entryBar,
        decimal entryPrice,
        int exitIdx,
        decimal exitRaw,
        int sessions,
        string reason,
        string exchange,
        ReversalBounceTradeSettings trade,
        decimal mfe,
        decimal mae,
        int deferrals)
    {
        var exitPrevLocked = IsFloorLocked(bars, exitIdx - 1, exchange);
        var exitSlipBps = trade.SlippageBaseBps + (exitPrevLocked ? trade.SlippageFloorLockPenaltyBps : 0m);
        var exitPrice = exitRaw * (1m - exitSlipBps / 10_000m);

        var gross = (exitPrice - entryPrice) / entryPrice * 100m;
        var feePct = trade.FeeBuyPercent + trade.FeeSellPercent + trade.TaxSellPercent;
        var net = gross - feePct;

        return new ReversalBounceFillResult(
            Entered: true,
            EntryDate: entryBar.Date,
            EntryPrice: Math.Round(entryPrice, 2),
            ExitDate: bars[exitIdx].Date,
            ExitPrice: Math.Round(exitPrice, 2),
            SessionsToExit: sessions,
            ExitReason: reason,
            ReturnPercentGross: Math.Round(gross, 2),
            ReturnPercentNet: Math.Round(net, 2),
            MaxFavorablePercent: Math.Round(mfe, 2),
            MaxAdversePercent: Math.Round(mae, 2),
            FloorDeferrals: deferrals);
    }

    private static ReversalBounceFillResult NoEntry(string reason) =>
        new(false, null, 0m, null, null, 0, reason, 0m, 0m, 0m, 0m, 0);

    private static bool IsFloorLocked(IReadOnlyList<OhlcvBar> bars, int index, string exchange)
    {
        if (index < 1 || index >= bars.Count)
            return false;
        var refPrice = bars[index - 1].Close;
        var (floor, _) = ExchangePriceBand.Calculate(refPrice, exchange);
        return ExchangePriceBand.IsLikelyFloorLocked(bars[index], floor);
    }
}
