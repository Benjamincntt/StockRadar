using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
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
        SellConfirmationTicks = 1,
    };

    public static MasterAlertPositionRecord Position(
        decimal entry = 100m,
        decimal peak = 100m,
        decimal? entryBarLow = null,
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
            null,
            null,
            null,
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
            anchor);

    /// <summary>Hộp Close dao động trong [boxLow, boxHigh], chạm đủ 2 cạnh, rồi vài phiên gãy xuống.</summary>
    internal static List<OhlcvBar> BuildBoxThenBreak(decimal boxLow, decimal boxHigh, int sessions)
    {
        var list = new List<OhlcvBar>();
        var day = new DateOnly(2026, 5, 4); // Monday
        for (var i = 0; i < sessions; i++)
        {
            var close = i % 2 == 0 ? boxLow : boxHigh;
            var open = (boxLow + boxHigh) / 2m;
            var high = Math.Min(boxHigh * 1.01m, close + 0.2m);
            var low = Math.Max(boxLow * 0.99m, close - 0.2m);
            list.Add(new OhlcvBar(day, open, high, low, close, 800_000));
            day = NextTradingDay(day);
        }

        // 3 phiên gãy
        var px = boxLow * 0.92m;
        for (var i = 0; i < 3; i++)
        {
            list.Add(new OhlcvBar(day, px, px * 1.01m, px * 0.99m, px, 900_000));
            day = NextTradingDay(day);
            px *= 0.99m;
        }

        return list;
    }

    private static DateOnly NextTradingDay(DateOnly d)
    {
        do
        {
            d = d.AddDays(1);
        } while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);

        return d;
    }
}
