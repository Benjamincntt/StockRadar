using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Infrastructure.MarketData;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Tests.SellExit;

internal static class SellExitFixtures
{
    public static readonly DateOnly EntryDate = new(2026, 7, 1);
    public static readonly DateOnly SellDate = new(2026, 7, 8); // >= 3 phiên giao dịch sau entry

    public static MasterAlertOptions Cfg() => new()
    {
        SellPoint1DropFromAnchorPercent = 4m,
        SellPoint2DropFromAnchorPercent = 6m,
        MinTradingSessionsToSell = 3,
        RiskWarningDrawdownFromPeakPercent = 4m,
        OverheadBaseBufferPercent = 0.5m,
        SellConfirmationTicks = 1,
        MarketPhaseMultipliers = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Favorable"] = 1.25m,
            ["Neutral"] = 1.0m,
            ["Unfavorable"] = 0.75m,
        },
    };

    public static MasterAlertPositionRecord Position(
        decimal entry = 100m,
        decimal peak = 100m,
        string? regime = MasterAlertExitRegimes.BlueSky,
        decimal? baseLow = null,
        decimal? baseHigh = null,
        decimal? entryBarLow = 95m,
        IReadOnlyList<string>? fired = null,
        DateOnly? entryDate = null) =>
        new(
            Guid.NewGuid(),
            "TEST",
            entryDate ?? EntryDate,
            entry,
            peak,
            1.0m,
            fired ?? [],
            "Neutral",
            false,
            null,
            regime,
            baseLow,
            baseHigh,
            entryBarLow,
            entryDate ?? EntryDate);

    public static KbsPriceBoardClient.KbsBoardRow Row(
        decimal close,
        decimal high = 0,
        decimal low = 0,
        decimal open = 0) =>
        new(
            "TEST",
            Open: open > 0 ? open : close,
            High: high > 0 ? high : close,
            Low: low > 0 ? low : close,
            Close: close,
            SessionVolume: 1_000_000,
            ChangePercent: 0,
            BidPrice1: close - 0.1m,
            BidPrice2: 0,
            BidPrice3: 0,
            AskPrice1: close + 0.1m,
            AskPrice2: 0,
            AskPrice3: 0,
            BidVolume1: 10_000,
            BidVolume2: 0,
            BidVolume3: 0,
            AskVolume1: 10_000,
            AskVolume2: 0,
            AskVolume3: 0,
            ForeignBuyVolume: 0,
            ForeignSellVolume: 0,
            ProprietaryVolume: 0,
            PutThroughVolume: 0,
            PutThroughValue: 0);

    public static string? Eval(
        MasterAlertPositionRecord pos,
        KbsPriceBoardClient.KbsBoardRow row,
        decimal anchor,
        string phase = "Neutral",
        DateOnly? session = null,
        MasterAlertOptions? cfg = null) =>
        TopOpportunityVipAlertEvaluator.EvaluatePositionSignal(
            cfg ?? Cfg(),
            pos,
            row,
            scan: null,
            session ?? SellDate,
            phase,
            anchor);
}
