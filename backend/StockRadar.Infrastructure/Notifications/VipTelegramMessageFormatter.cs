using System.Globalization;
using System.Net;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>Telegram VIP — HTML parse_mode, phân bậc đọc nhanh trong phiên.</summary>
internal static class VipTelegramMessageFormatter
{
    private const string Sep = "──────────────────────";

    public static string FormatEntryReady(
        DailyOpportunityRecord opp,
        EntryPointDto entry,
        KbsPriceBoardClient.KbsBoardRow row)
    {
        var low = Math.Min(entry.BaseLow, entry.EntryPrice);
        var high = Math.Max(entry.EntryPrice, entry.TriggerPrice);
        var dna = FormatDna(opp.SetupDna, entry.Headline);

        return
            $"🎯 <b>[STOCKRADAR] ENTRY READY: {E(opp.Symbol)}</b>\n" +
            $"{Sep}\n" +
            $"• <b>Giá hiện tại:</b> {F(row.Close)} ({Signed(row.ChangePercent)})\n" +
            $"• <b>Vùng mua AI:</b> {F(low)} – {F(high)}\n\n" +
            $"📈 <b>Xếp hạng &amp; Setup:</b>\n" +
            $"• #Top{opp.Rank} Opportunities (Buy Score: {opp.BuyScore})\n" +
            $"• Setup DNA: <code>{E(dna)}</code>";
    }

    public static string FormatBuyPoint1(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        MasterAlertOptions cfg)
    {
        var volNote = row.SessionVolume >= cfg.MinSessionVolume
            ? " (Vượt ngưỡng đạt chuẩn)"
            : "";

        return
            $"🚀 <b>[STOCKRADAR] MASTER ALERT: MUA ĐIỂM 1 ({E(opp.Symbol)})</b>\n" +
            $"{Sep}\n" +
            $"• <b>Giá khớp:</b> {F(row.Close)} 🟢 ({Signed(row.ChangePercent)})\n" +
            $"• <b>Khối lượng:</b> {VolM(row.SessionVolume)}{volNote}\n\n" +
            $"📊 <b>Vị thế hệ thống:</b>\n" +
            $"• #Top{opp.Rank} Opportunities | Buy Score: {opp.BuyScore}\n" +
            $"• Trạng thái: <code>{E(StatusLabel(opp))}</code>";
    }

    public static string FormatBuyPoint2(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        MasterAlertSessionTracker.SymbolMasterState state)
    {
        var fromM1 = GainFromM1Percent(state.BuyPoint1Price, row.Close);

        return
            $"🔥 <b>[STOCKRADAR] MASTER ALERT: MUA ĐIỂM 2 ({E(opp.Symbol)})</b>\n" +
            $"{Sep}\n" +
            $"• <b>Giá khớp:</b> {F(row.Close)} 🔥 ({Signed(row.ChangePercent)})\n" +
            $"• <b>Khối lượng:</b> {VolM(row.SessionVolume)}\n" +
            $"• <b>Hiệu suất từ M1:</b> {Signed(fromM1)} (Đỉnh cao nhất)\n\n" +
            $"⚡ <b>Ghi chú:</b> Gia tăng vị thế thuận xu hướng.";
    }

    public static string FormatCutLoss1(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        MasterAlertSessionTracker.SymbolMasterState state)
    {
        var peak = state.PeakGainPercent();

        return
            $"⚠️ <b>[STOCKRADAR] WARNING: CẮT LỖ ĐIỂM 1 ({E(opp.Symbol)})</b>\n" +
            $"{Sep}\n" +
            $"• <b>Giá hiện tại:</b> {F(row.Close)} 🔴 (Phiên {Signed(row.ChangePercent)})\n" +
            $"• <b>Khối lượng:</b> {VolM(row.SessionVolume)}\n" +
            $"• <b>Độ sụt giảm:</b> Quay đầu từ đỉnh M1 ({Signed(peak)})\n\n" +
            $"🛑 <b>Hành động:</b> Hạ tỷ trọng 1/2 vị thế theo quy tắc quản trị rủi ro.";
    }

    public static string FormatCutAll(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        MasterAlertSessionTracker.SymbolMasterState state)
    {
        var peak = state.PeakGainPercent();

        return
            $"🛑 <b>[STOCKRADAR] WARNING: CẮT HẾT ({E(opp.Symbol)})</b>\n" +
            $"{Sep}\n" +
            $"• <b>Giá hiện tại:</b> {F(row.Close)} 🔴 (Phiên {Signed(row.ChangePercent)})\n" +
            $"• <b>Khối lượng:</b> {VolM(row.SessionVolume)}\n" +
            $"• <b>Đỉnh từ M1:</b> {Signed(peak)} · Phân phối xác nhận\n\n" +
            $"🛑 <b>Hành động:</b> Thoát toàn bộ vị thế theo quy tắc quản trị rủi ro.";
    }

    public static string FormatMaster(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        string signalKey,
        MasterAlertSessionTracker.SymbolMasterState state,
        MasterAlertOptions cfg) => signalKey switch
    {
        MasterAlertKinds.BuyPoint1 => FormatBuyPoint1(opp, row, cfg),
        MasterAlertKinds.BuyPoint2 => FormatBuyPoint2(opp, row, state),
        MasterAlertKinds.CutLoss1 => FormatCutLoss1(opp, row, state),
        MasterAlertKinds.CutAll => FormatCutAll(opp, row, state),
        _ => FormatBuyPoint1(opp, row, cfg),
    };

    private static string StatusLabel(DailyOpportunityRecord opp)
    {
        if (!string.IsNullOrWhiteSpace(opp.TradeStateReason))
            return opp.TradeStateReason;

        return opp.SetupDna ?? "Top cơ hội";
    }

    private static string FormatDna(string? setupDna, string headline)
    {
        if (!string.IsNullOrWhiteSpace(setupDna) && !string.IsNullOrWhiteSpace(headline))
            return $"{setupDna} + {headline}";

        return setupDna ?? headline ?? "—";
    }

    private static decimal GainFromM1Percent(decimal buy1Price, decimal current)
    {
        if (buy1Price <= 0)
            return 0;

        return Math.Round((current - buy1Price) / buy1Price * 100m, 2);
    }

    private static string F(decimal value) =>
        value.ToString("N1", CultureInfo.InvariantCulture);

    private static string Signed(decimal pct) =>
        (pct >= 0 ? "+" : "") + pct.ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private static string VolM(long volume)
    {
        var m = volume / 1_000_000m;
        return m.ToString("0.##", CultureInfo.InvariantCulture) + "M";
    }

    private static string E(string? text) => WebUtility.HtmlEncode(text ?? "");
}
