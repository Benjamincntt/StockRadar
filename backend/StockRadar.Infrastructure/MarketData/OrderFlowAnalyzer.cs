using StockRadar.Application.Options;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>Phát hiện lệnh đột biến từ snapshot KBS (FB/FS/CV/V1/U1).</summary>
internal sealed class OrderFlowAnalyzer
{
    public IReadOnlyList<OrderFlowEvent> Detect(
        KbsPriceBoardClient.KbsBoardRow current,
        KbsPriceBoardClient.KbsBoardRow? previous,
        OpportunityMonitorOptions cfg)
    {
        if (previous is null)
            return [];

        var events = new List<OrderFlowEvent>();
        var dForeignBuy = current.ForeignBuyVolume - previous.ForeignBuyVolume;
        var dForeignSell = current.ForeignSellVolume - previous.ForeignSellVolume;
        var dProp = current.ProprietaryVolume - previous.ProprietaryVolume;
        var dPutThrough = current.PutThroughVolume - previous.PutThroughVolume;
        var dBid1 = current.BidVolume1 - previous.BidVolume1;
        var dAsk1 = current.AskVolume1 - previous.AskVolume1;

        if (dForeignBuy >= cfg.MinForeignFlowDelta && dForeignBuy >= dForeignSell)
        {
            events.Add(new OrderFlowEvent(
                OrderFlowSource.ForeignBuy,
                current.Close,
                dForeignBuy,
                current.ForeignBuyVolume,
                current.BidPrice1,
                current.BidVolume1,
                BuildForeignMessage(current, dForeignBuy, isBuy: true)));
        }

        if (dForeignSell >= cfg.MinForeignFlowDelta && dForeignSell > dForeignBuy)
        {
            events.Add(new OrderFlowEvent(
                OrderFlowSource.ForeignSell,
                current.Close,
                dForeignSell,
                current.ForeignSellVolume,
                current.AskPrice1,
                current.AskVolume1,
                BuildForeignMessage(current, dForeignSell, isBuy: false)));
        }

        if (dProp >= cfg.MinProprietaryDelta
            && dProp > dForeignBuy
            && dProp > dForeignSell)
        {
            events.Add(new OrderFlowEvent(
                OrderFlowSource.Proprietary,
                current.Close,
                dProp,
                current.ProprietaryVolume,
                current.Close,
                0,
                $"Tự doanh tăng thêm {dProp:N0} CP | Lũy kế {current.ProprietaryVolume:N0}\n" +
                $"Giá {current.Close:N1} | Mua1 {current.BidPrice1:N1} ({current.BidVolume1:N0}) | Bán1 {current.AskPrice1:N1} ({current.AskVolume1:N0})"));
        }

        if (dPutThrough >= cfg.MinPutThroughDelta)
        {
            var valueLine = current.PutThroughValue > 0
                ? $"\nGiá trị lũy kế {FormatVnd(current.PutThroughValue)}"
                : "";
            events.Add(new OrderFlowEvent(
                OrderFlowSource.PutThrough,
                current.Close,
                dPutThrough,
                current.PutThroughVolume,
                current.Close,
                0,
                $"Thỏa thuận +{dPutThrough:N0} CP | Lũy kế {current.PutThroughVolume:N0} CP @ {current.Close:N1}{valueLine}"));
        }

        if (current.BidVolume1 >= cfg.MinHangVolume && dBid1 >= cfg.MinHangVolumeDelta)
        {
            events.Add(new OrderFlowEvent(
                OrderFlowSource.LargeBid,
                current.BidPrice1,
                dBid1,
                current.BidVolume1,
                current.BidPrice1,
                current.BidVolume1,
                $"Lệnh treo MUA lớn @ {current.BidPrice1:N1}\n" +
                $"Tăng thêm {dBid1:N0} CP | Tổng chờ mua 1: {current.BidVolume1:N0}"));
        }

        if (current.AskVolume1 >= cfg.MinHangVolume && dAsk1 >= cfg.MinHangVolumeDelta)
        {
            events.Add(new OrderFlowEvent(
                OrderFlowSource.LargeAsk,
                current.AskPrice1,
                dAsk1,
                current.AskVolume1,
                current.AskPrice1,
                current.AskVolume1,
                $"Lệnh treo BÁN lớn @ {current.AskPrice1:N1}\n" +
                $"Tăng thêm {dAsk1:N0} CP | Tổng chờ bán 1: {current.AskVolume1:N0}"));
        }

        return events;
    }

    private static string BuildForeignMessage(KbsPriceBoardClient.KbsBoardRow row, long delta, bool isBuy)
    {
        var side = isBuy ? "MUA" : "BÁN";
        var total = isBuy ? row.ForeignBuyVolume : row.ForeignSellVolume;
        return $"Khối ngoại {side} thêm {delta:N0} CP | Lũy kế NN {(isBuy ? "mua" : "bán")} {total:N0}\n" +
               $"Giá {row.Close:N1} | Mua1 {row.BidPrice1:N1} ({row.BidVolume1:N0}) | Bán1 {row.AskPrice1:N1} ({row.AskVolume1:N0})";
    }

    private static string FormatVnd(long vnd) =>
        vnd >= 1_000_000_000
            ? $"{vnd / 1_000_000_000m:0.##} tỷ"
            : vnd >= 1_000_000
                ? $"{vnd / 1_000_000m:0.#} triệu"
                : $"{vnd:N0} đ";
}

internal enum OrderFlowSource
{
    ForeignBuy,
    ForeignSell,
    Proprietary,
    PutThrough,
    LargeBid,
    LargeAsk
}

internal sealed record OrderFlowEvent(
    OrderFlowSource Source,
    decimal Price,
    long DeltaVolume,
    long CumulativeVolume,
    decimal OrderPrice,
    long OrderVolume,
    string Message);

internal static class OrderFlowSourceLabels
{
    public static string Label(OrderFlowSource source) => source switch
    {
        OrderFlowSource.ForeignBuy => "Khối ngoại mua",
        OrderFlowSource.ForeignSell => "Khối ngoại bán",
        OrderFlowSource.Proprietary => "Tự doanh",
        OrderFlowSource.PutThrough => "Thỏa thuận",
        OrderFlowSource.LargeBid => "Lệnh treo mua",
        OrderFlowSource.LargeAsk => "Lệnh treo bán",
        _ => "Lệnh đột biến"
    };

    public const string SourceTag = "Trong phiên";

    public static bool IsBuySide(OrderFlowSource source) =>
        source is OrderFlowSource.ForeignBuy or OrderFlowSource.LargeBid;
}
