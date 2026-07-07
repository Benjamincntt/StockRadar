using System.Globalization;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>Telegram VIP — một dòng, đọc trong 1 giây.</summary>
internal static class VipTelegramMessageFormatter
{
    public static string FormatEntryReady(
        DailyOpportunityRecord opp,
        EntryPointDto entry,
        KbsPriceBoardClient.KbsBoardRow row)
    {
        var low = Math.Min(entry.BaseLow, entry.EntryPrice);
        var high = Math.Max(entry.EntryPrice, entry.TriggerPrice);
        return $"{opp.Symbol}: vào vùng mua - giá {F(row.Close)} ({F(low)}–{F(high)})";
    }

    public static string FormatBuyPoint1(DailyOpportunityRecord opp, KbsPriceBoardClient.KbsBoardRow row) =>
        $"{opp.Symbol}: mua 1 nửa - đã tăng {Pct(row.ChangePercent)} từ đỉnh nền";

    public static string FormatBuyPoint2(DailyOpportunityRecord opp, KbsPriceBoardClient.KbsBoardRow row) =>
        $"{opp.Symbol}: mua hết - đã tăng {Pct(row.ChangePercent)} từ đỉnh nền hôm nay";

    public static string FormatCutLoss1(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        MasterAlertSessionTracker.SymbolMasterState state) =>
        $"{opp.Symbol}: cắt 1 nửa - giảm {Pct(DropFromPeakPercent(state, row))} từ đỉnh gần nhất";

    public static string FormatCutAll(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        MasterAlertSessionTracker.SymbolMasterState state) =>
        $"{opp.Symbol}: đóng vị thế - giảm {Pct(DropFromPeakPercent(state, row))} từ đỉnh gần nhất";

    public static string FormatMaster(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        string signalKey,
        MasterAlertSessionTracker.SymbolMasterState state,
        MasterAlertOptions _) => signalKey switch
    {
        MasterAlertKinds.BuyPoint1 => FormatBuyPoint1(opp, row),
        MasterAlertKinds.BuyPoint2 => FormatBuyPoint2(opp, row),
        MasterAlertKinds.CutLoss1 => FormatCutLoss1(opp, row, state),
        MasterAlertKinds.CutAll => FormatCutAll(opp, row, state),
        _ => FormatBuyPoint1(opp, row),
    };

    private static decimal DropFromPeakPercent(
        MasterAlertSessionTracker.SymbolMasterState state,
        KbsPriceBoardClient.KbsBoardRow row)
    {
        var peak = state.SessionHighSinceBuy1;
        if (peak <= 0 || row.Close <= 0 || row.Close >= peak)
            return 0;

        return Math.Round((peak - row.Close) / peak * 100m, 1);
    }

    private static string F(decimal value) =>
        value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string Pct(decimal value) =>
        Math.Abs(value).ToString("0.#", CultureInfo.InvariantCulture) + "%";
}
