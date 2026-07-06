using StockRadar.Domain.Enums;

namespace StockRadar.Domain.Services;

public static class TradeStateLabels
{
    public static string ToVi(StockTradeState state) => state switch
    {
        StockTradeState.Avoid => "Tránh",
        StockTradeState.Watchlist => "Theo dõi",
        StockTradeState.AwaitingTrigger => "Chờ kích hoạt",
        StockTradeState.Actionable => "Vào ngay",
        _ => state.ToString()
    };

    public static BuyRecommendation ToLegacyRecommendation(StockTradeState state, int buyScore) =>
        state switch
        {
            StockTradeState.Actionable when buyScore >= 80 => BuyRecommendation.StrongBuy,
            StockTradeState.Actionable => BuyRecommendation.Watch,
            StockTradeState.AwaitingTrigger or StockTradeState.Watchlist => BuyRecommendation.Watch,
            _ => BuyRecommendation.Avoid
        };
}
