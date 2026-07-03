using StockRadar.Application.Options;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>Phát hiện khớp lệnh từ delta KL phiên giữa 2 lần quét bảng giá KBS.</summary>
internal sealed class TradePrintDetector
{
    public TradePrint? Detect(
        KbsPriceBoardClient.KbsBoardRow current,
        KbsPriceBoardClient.KbsBoardRow? previous,
        OpportunityMonitorOptions cfg)
    {
        if (previous is null || current.Close <= 0)
            return null;

        var delta = current.SessionVolume - previous.SessionVolume;
        if (delta < cfg.MinTradeVolume)
            return null;

        var valueVnd = current.Close * 1000m * delta;
        if (valueVnd < cfg.MinTradeValueVnd)
            return null;

        var side = InferSide(current, previous);
        return new TradePrint(current.Symbol, side, current.Close, delta);
    }

    private static string InferSide(
        KbsPriceBoardClient.KbsBoardRow current,
        KbsPriceBoardClient.KbsBoardRow previous)
    {
        if (current.Close > previous.Close)
            return "Buy";
        if (current.Close < previous.Close)
            return "Sell";

        if (current.AskPrice1 > 0 && current.Close >= current.AskPrice1)
            return "Buy";
        if (current.BidPrice1 > 0 && current.Close <= current.BidPrice1)
            return "Sell";

        return current.ChangePercent >= 0 ? "Buy" : "Sell";
    }

    internal sealed record TradePrint(string Symbol, string Side, decimal Price, long Volume);
}
