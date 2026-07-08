using System.Globalization;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>Telegram VIP — một dòng, emoji + bold mã + Vol.</summary>
internal static class VipTelegramMessageFormatter
{
    public static string FormatEntryReady(
        DailyOpportunityRecord opp,
        EntryPointDto entry,
        KbsPriceBoardClient.KbsBoardRow row) =>
        $"🎯 <b>{opp.Symbol}</b>: Entry Ready — Giá <code>{F(row.Close)}</code> đã lọt vùng mua AI (Vol: {VolM(row.SessionVolume)})";

    public static string FormatBuyPoint1(
        DailyOpportunityRecord opp,
        EntryPointDto? entry,
        KbsPriceBoardClient.KbsBoardRow row)
    {
        var gain = TopOpportunityVipAlertEvaluator.GainFromBasePeakPercent(entry, row.Close);
        return $"🟢 <b>{opp.Symbol}</b>: Mua 1 nửa — Tăng {SignedPlus(gain)} từ đỉnh nền (Vol: {VolM(row.SessionVolume)})";
    }

    public static string FormatBuyPoint2(
        DailyOpportunityRecord opp,
        EntryPointDto? entry,
        KbsPriceBoardClient.KbsBoardRow row)
    {
        var gain = TopOpportunityVipAlertEvaluator.GainFromBasePeakPercent(entry, row.Close);
        return $"🔥 <b>{opp.Symbol}</b>: Mua hết — Tăng {SignedPlus(gain)} bứt phá hôm nay (Vol: {VolM(row.SessionVolume)})";
    }

    public static string FormatCutLoss1(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        MasterAlertSessionTracker.SymbolMasterState state) =>
        $"🟡 <b>{opp.Symbol}</b>: Cắt 1 nửa — Giảm {SignedMinus(DropFromPeakPercent(state, row))} từ đỉnh gần nhất (Vol: {VolM(row.SessionVolume)})";

    public static string FormatCutAll(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        MasterAlertSessionTracker.SymbolMasterState state) =>
        $"🔴 <b>{opp.Symbol}</b>: Đóng vị thế — Giảm {SignedMinus(DropFromPeakPercent(state, row))} vi phạm cắt lỗ! (Vol: {VolM(row.SessionVolume)})";

    public static string FormatMaster(
        DailyOpportunityRecord opp,
        EntryPointDto? entry,
        KbsPriceBoardClient.KbsBoardRow row,
        string signalKey,
        MasterAlertSessionTracker.SymbolMasterState state,
        MasterAlertOptions _) => signalKey switch
    {
        MasterAlertKinds.BuyPoint1 => FormatBuyPoint1(opp, entry, row),
        MasterAlertKinds.BuyPoint2 => FormatBuyPoint2(opp, entry, row),
        MasterAlertKinds.CutLoss1 => FormatCutLoss1(opp, row, state),
        MasterAlertKinds.CutAll => FormatCutAll(opp, row, state),
        _ => FormatBuyPoint1(opp, entry, row),
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

    private static string SignedPlus(decimal pct) =>
        "+" + Math.Abs(pct).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static string SignedMinus(decimal pct) =>
        "-" + Math.Abs(pct).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static string VolM(long volume)
    {
        var m = volume / 1_000_000m;
        return m.ToString("0.#", CultureInfo.InvariantCulture) + "M";
    }
}
