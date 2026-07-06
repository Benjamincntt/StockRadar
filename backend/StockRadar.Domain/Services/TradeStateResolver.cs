using StockRadar.Domain.Enums;

namespace StockRadar.Domain.Services;

public sealed record TradeStateListContext(bool OnOpportunityList);

public sealed record TradeStateResult(StockTradeState State, string Reason);

/// <summary>Gộp gate + entry + list context → một TradeState duy nhất.</summary>
public static class TradeStateResolver
{
    private const int StrongBuyMinScore = 80;

    public static TradeStateResult Resolve(
        EntryPointEvaluation entry,
        string? gateFailure,
        int buyScore,
        TradeStateListContext? listContext = null)
    {
        var onList = listContext?.OnOpportunityList == true;

        if (entry.Status is EntryPointStatus.Late or EntryPointStatus.Invalid)
            return new(StockTradeState.Avoid, entry.Headline);

        if (gateFailure is not null)
        {
            if (IsSevereGate(gateFailure))
                return new(StockTradeState.Avoid, gateFailure);

            if (IsNoBreakoutGate(gateFailure))
            {
                if (onList)
                    return new(StockTradeState.Watchlist, gateFailure);

                return new(StockTradeState.Avoid, "Không đạt tiêu chí tối thiểu");
            }

            return new(StockTradeState.AwaitingTrigger, gateFailure);
        }

        if (entry.Status == EntryPointStatus.Ready)
        {
            var reason = buyScore >= StrongBuyMinScore
                ? "Mua mạnh — đạt chuẩn SmartMoney"
                : "Đạt chuẩn SmartMoney";
            return new(StockTradeState.Actionable, reason);
        }

        if (entry.Status == EntryPointStatus.Watch && onList)
            return new(StockTradeState.Watchlist, ResolveWatchlistReason(entry));

        return new(StockTradeState.Avoid, "Không đạt tiêu chí tối thiểu");
    }

    private static string ResolveWatchlistReason(EntryPointEvaluation entry) =>
        entry.Headline.Contains("phá vỡ", StringComparison.OrdinalIgnoreCase)
        || entry.Headline.Contains(BasePriceLabels.Breakout, StringComparison.OrdinalIgnoreCase)
            ? "Chưa phá vỡ nền / Chờ phiên kích hoạt"
            : entry.Headline;

    private static bool IsSevereGate(string gate) =>
        gate.Contains("lịch sử", StringComparison.OrdinalIgnoreCase)
        || gate.Contains("Thanh khoản", StringComparison.OrdinalIgnoreCase)
        || gate.Contains("phân phối", StringComparison.OrdinalIgnoreCase)
        || gate.Contains("FOMO", StringComparison.OrdinalIgnoreCase);

    private static bool IsNoBreakoutGate(string gate) =>
        gate.Contains("Chưa phá", StringComparison.OrdinalIgnoreCase)
        || gate.Contains(BasePriceLabels.Breakout, StringComparison.OrdinalIgnoreCase)
            && gate.Contains("Chưa", StringComparison.OrdinalIgnoreCase);
}
