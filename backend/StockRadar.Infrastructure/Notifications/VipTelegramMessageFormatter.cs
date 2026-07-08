using System.Globalization;
using System.Text;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>Telegram VIP — emoji + bold mã + lý do (HTML).</summary>
internal static class VipTelegramMessageFormatter
{
    public static string FormatEntryReady(
        DailyOpportunityRecord opp,
        EntryPointDto entry,
        KbsPriceBoardClient.KbsBoardRow row,
        string? reasoning = null)
    {
        var high = Math.Max(entry.EntryPrice, entry.TriggerPrice);
        var sb = new StringBuilder();
        sb.Append($"🎯 <b>{opp.Symbol}</b>: Entry Ready\n");
        sb.Append($"Giá <code>{F(row.Close)}</code> lọt vùng <code>{F(entry.BaseLow)}</code>-<code>{F(high)}</code>");
        AppendReasoning(sb, reasoning);
        sb.Append($"\nVol: {VolM(row.SessionVolume)}");
        return sb.ToString();
    }

    public static string FormatBuyPoint1(
        DailyOpportunityRecord opp,
        EntryPointDto? entry,
        KbsPriceBoardClient.KbsBoardRow row,
        string? reasoning = null)
    {
        var gain = TopOpportunityVipAlertEvaluator.GainFromBasePeakPercent(entry, row.Close);
        var sb = new StringBuilder();
        sb.Append($"🟢 <b>{opp.Symbol}</b>: Mua 1 nửa\n");
        sb.Append($"Tăng {SignedPlus(gain)} từ đỉnh nền");
        AppendReasoning(sb, reasoning);
        sb.Append($"\nVol: {VolM(row.SessionVolume)}");
        return sb.ToString();
    }

    public static string FormatBuyPoint2(
        DailyOpportunityRecord opp,
        EntryPointDto? entry,
        KbsPriceBoardClient.KbsBoardRow row,
        string? reasoning = null)
    {
        var gain = TopOpportunityVipAlertEvaluator.GainFromBasePeakPercent(entry, row.Close);
        var sb = new StringBuilder();
        sb.Append($"🔥 <b>{opp.Symbol}</b>: Mua hết\n");
        sb.Append($"Tăng {SignedPlus(gain)} bứt phá từ đỉnh nền");
        AppendReasoning(sb, reasoning);
        sb.Append($"\nVol: {VolM(row.SessionVolume)}");
        return sb.ToString();
    }

    public static string FormatCutLoss1(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        MasterAlertSessionTracker.SymbolMasterState state,
        string? reasoning = null)
    {
        var peak = state.PeakGainPercent();
        var sb = new StringBuilder();
        sb.Append($"🟡 <b>{opp.Symbol}</b>: Bán 1 nửa\n");
        sb.Append($"Peak đã đạt {SignedPlus(peak)}");
        AppendReasoning(sb, reasoning);
        sb.Append($"\nVol: {VolM(row.SessionVolume)}");
        return sb.ToString();
    }

    public static string FormatCutAll(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        MasterAlertSessionTracker.SymbolMasterState state,
        string? reasoning = null)
    {
        var peak = state.PeakGainPercent();
        var sb = new StringBuilder();
        sb.Append($"🔴 <b>{opp.Symbol}</b>: Bán hết\n");
        sb.Append($"Peak đã đạt {SignedPlus(peak)}");
        AppendReasoning(sb, reasoning);
        sb.Append($"\nVol: {VolM(row.SessionVolume)}");
        return sb.ToString();
    }

    public static string FormatMaster(
        DailyOpportunityRecord opp,
        EntryPointDto? entry,
        KbsPriceBoardClient.KbsBoardRow row,
        string signalKey,
        MasterAlertSessionTracker.SymbolMasterState state,
        MasterAlertOptions _,
        string? reasoning = null) => signalKey switch
    {
        MasterAlertKinds.BuyPoint1 => FormatBuyPoint1(opp, entry, row, reasoning),
        MasterAlertKinds.BuyPoint2 => FormatBuyPoint2(opp, entry, row, reasoning),
        MasterAlertKinds.CutLoss1 => FormatCutLoss1(opp, row, state, reasoning),
        MasterAlertKinds.CutAll => FormatCutAll(opp, row, state, reasoning),
        _ => FormatBuyPoint1(opp, entry, row, reasoning),
    };

    internal static string F(decimal value) =>
        value.ToString("0.#", CultureInfo.InvariantCulture);

    private static void AppendReasoning(StringBuilder sb, string? reasoning)
    {
        if (string.IsNullOrWhiteSpace(reasoning))
            return;

        sb.Append('\n');
        sb.Append(reasoning);
    }

    private static string SignedPlus(decimal pct) =>
        "+" + Math.Abs(pct).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private static string VolM(long volume)
    {
        var m = volume / 1_000_000m;
        return m.ToString("0.#", CultureInfo.InvariantCulture) + "M";
    }
}
