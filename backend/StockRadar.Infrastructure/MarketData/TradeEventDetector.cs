using StockRadar.Application.DTOs;
using StockRadar.Application.Options;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>Phát hiện lô lớn + nhãn VSA từ delta KL/giá giữa 2 lần quét KBS.</summary>
internal sealed class TradeEventDetector
{
    public DetectedTradeEvent? DetectScan(
        KbsPriceBoardClient.KbsBoardRow current,
        KbsPriceBoardClient.KbsBoardRow? previous,
        OpportunityMonitorOptions cfg)
    {
        if (previous is null || current.Close <= 0)
            return null;

        var deltaVol = current.SessionVolume - previous.SessionVolume;
        if (deltaVol <= 0)
            return null;

        var valueVnd = current.Close * 1000m * deltaVol;
        var isMicro = deltaVol >= cfg.MinMicroVolume && valueVnd >= cfg.MinMicroValueVnd;
        var isBlock = deltaVol >= cfg.MinTradeVolume && valueVnd >= cfg.MinTradeValueVnd;
        if (!isMicro && !isBlock)
            return null;

        var deltaPrice = current.Close - previous.Close;
        var spreadPct = previous.Close > 0
            ? Math.Round(Math.Abs(deltaPrice) / previous.Close * 100m, 3)
            : 0m;

        var label = ClassifyLabel(spreadPct, deltaPrice, cfg);
        var bookImbalance = OrderBookMetrics.BookImbalance(current);
        var foreignNetDelta = OrderBookMetrics.ForeignNetDelta(current, previous);
        var propDelta = OrderBookMetrics.PropDelta(current, previous);

        return new DetectedTradeEvent(
            current.Symbol,
            label,
            current.Close,
            deltaVol,
            valueVnd,
            spreadPct,
            bookImbalance,
            foreignNetDelta,
            propDelta,
            isBlock);
    }

    internal static string ClassifyLabel(
        decimal spreadPct,
        decimal deltaPrice,
        OpportunityMonitorOptions cfg)
    {
        if (spreadPct < cfg.VsaSpreadTightPercent)
            return TradeEventLabels.GomIm;

        if (spreadPct >= cfg.VsaSpreadWidePercent)
            return deltaPrice > 0
                ? TradeEventLabels.DayGia
                : deltaPrice < 0
                    ? TradeEventLabels.Xa
                    : TradeEventLabels.TrungTinh;

        return TradeEventLabels.TrungTinh;
    }

    internal sealed record DetectedTradeEvent(
        string Symbol,
        string Label,
        decimal Price,
        long Volume,
        decimal ValueVnd,
        decimal SpreadPct,
        long BookImbalance,
        long ForeignNetDelta,
        long PropDelta,
        bool IsImmediateBlock);
}
