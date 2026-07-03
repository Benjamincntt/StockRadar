using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.MarketData;

internal static class OrderBookMetrics
{
    public static long BidDepth(KbsPriceBoardClient.KbsBoardRow row) =>
        row.BidVolume1 + row.BidVolume2 + row.BidVolume3;

    public static long AskDepth(KbsPriceBoardClient.KbsBoardRow row) =>
        row.AskVolume1 + row.AskVolume2 + row.AskVolume3;

    public static long BookImbalance(KbsPriceBoardClient.KbsBoardRow row) =>
        BidDepth(row) - AskDepth(row);

    public static long ForeignNetDelta(
        KbsPriceBoardClient.KbsBoardRow current,
        KbsPriceBoardClient.KbsBoardRow? previous)
    {
        if (previous is null)
            return 0;

        var buy = current.ForeignBuyVolume - previous.ForeignBuyVolume;
        var sell = current.ForeignSellVolume - previous.ForeignSellVolume;
        return buy - sell;
    }

    public static long PropDelta(
        KbsPriceBoardClient.KbsBoardRow current,
        KbsPriceBoardClient.KbsBoardRow? previous)
    {
        if (previous is null)
            return 0;

        return current.ProprietaryVolume - previous.ProprietaryVolume;
    }
}
