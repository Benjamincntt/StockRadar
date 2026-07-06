namespace StockRadar.Domain.Enums;

/// <summary>Một nhãn hành động duy nhất cho mọi màn hình (list, chi tiết, alert).</summary>
public enum StockTradeState
{
    Avoid,
    Watchlist,
    AwaitingTrigger,
    Actionable
}
