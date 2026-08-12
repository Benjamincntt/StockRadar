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
        var gainFromOpen = TopOpportunityVipAlertEvaluator.GainFromOpenPercent(row.Open, row.Close);
        var sb = new StringBuilder();
        sb.Append($"🎯 <b>{opp.Symbol}</b>: Entry Ready\n");
        sb.Append($"Giá <code>{F(row.Close)}</code> lọt vùng <code>{F(entry.BaseLow)}</code>-<code>{F(high)}</code>");
        AppendReasoning(sb, reasoning);
        sb.Append(
            $"\nP&amp;L phiên: {SignedPct(gainFromOpen)} " +
            $"(<code>{F(row.Open)}</code> → <code>{F(row.Close)}</code>)");
        sb.Append($"\nVol: {VolM(row.SessionVolume)}");
        return sb.ToString();
    }

    public static string FormatBuyPoint1(
        DailyOpportunityRecord opp,
        EntryPointDto? entry,
        KbsPriceBoardClient.KbsBoardRow row,
        decimal slippageBufferPercent,
        string? reasoning = null,
        string? buyTriggerBranch = null)
    {
        var gainFromOpen = TopOpportunityVipAlertEvaluator.GainFromOpenPercent(row.Open, row.Close);
        var sb = new StringBuilder();
        sb.Append($"🟢 <b>{opp.Symbol}</b>: Mua 1 nửa\n");
        if (string.Equals(
                buyTriggerBranch,
                TopOpportunityVipAlertEvaluator.BuyTriggerDipBounce,
                StringComparison.Ordinal))
            sb.Append($"Dip-bounce (bull-trap) · P&amp;L phiên {SignedPct(gainFromOpen)}");
        else if (string.Equals(
                buyTriggerBranch,
                TopOpportunityVipAlertEvaluator.BuyTriggerPullback,
                StringComparison.Ordinal))
            sb.Append($"Hồi sát MA · P&amp;L phiên {SignedPct(gainFromOpen)}");
        else
            sb.Append($"Breakout · P&amp;L phiên {SignedPct(gainFromOpen)}");
        AppendReasoning(sb, reasoning);
        AppendSlippageBuffer(sb, entry, slippageBufferPercent);
        sb.Append($"\nVol: {VolM(row.SessionVolume)}");
        return sb.ToString();
    }

    public static string FormatBuyPoint2(
        DailyOpportunityRecord opp,
        EntryPointDto? entry,
        KbsPriceBoardClient.KbsBoardRow row,
        decimal slippageBufferPercent,
        string? reasoning = null,
        string? buyTriggerBranch = null)
    {
        var gainFromOpen = TopOpportunityVipAlertEvaluator.GainFromOpenPercent(row.Open, row.Close);
        var sb = new StringBuilder();
        sb.Append($"🔥 <b>{opp.Symbol}</b>: Mua hết\n");
        if (string.Equals(
                buyTriggerBranch,
                TopOpportunityVipAlertEvaluator.BuyTriggerScaleIn,
                StringComparison.Ordinal))
            sb.Append($"Scale-in (+lãi Buy1) · P&amp;L phiên {SignedPct(gainFromOpen)}");
        else
            sb.Append($"Bứt phá · P&amp;L phiên {SignedPct(gainFromOpen)}");
        AppendReasoning(sb, reasoning);
        AppendSlippageBuffer(sb, entry, slippageBufferPercent);
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
        sb.Append($"Peak so entry {SignedPct(peak)}");
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
        sb.Append($"Peak so entry {SignedPct(peak)}");
        AppendReasoning(sb, reasoning);
        sb.Append($"\nVol: {VolM(row.SessionVolume)}");
        return sb.ToString();
    }

    public static string FormatSellHalf(
        string symbol,
        decimal peakGain,
        decimal currentGain,
        KbsPriceBoardClient.KbsBoardRow row,
        string? reasoning = null)
    {
        var sb = new StringBuilder();
        sb.Append($"🟡 <b>{symbol}</b>: Bán 1 nửa\n");
        sb.Append($"Peak so entry {SignedPct(peakGain)} · hiện {SignedPct(currentGain)}");
        AppendReasoning(sb, reasoning);
        sb.Append($"\nVol: {VolM(row.SessionVolume)}");
        return sb.ToString();
    }

    public static string FormatSellAll(
        string symbol,
        decimal peakGain,
        decimal currentGain,
        KbsPriceBoardClient.KbsBoardRow row,
        string? reasoning = null)
    {
        var sb = new StringBuilder();
        sb.Append($"🔴 <b>{symbol}</b>: Bán hết\n");
        sb.Append($"Peak so entry {SignedPct(peakGain)} · hiện {SignedPct(currentGain)}");
        AppendReasoning(sb, reasoning);
        sb.Append($"\nVol: {VolM(row.SessionVolume)}");
        return sb.ToString();
    }

    public static string FormatRiskWarning(
        string symbol,
        decimal drawdownFromAnchor,
        decimal currentGain,
        KbsPriceBoardClient.KbsBoardRow row,
        string? reasoning = null)
    {
        var sb = new StringBuilder();
        sb.Append($"⚠️ <b>{symbol}</b>: CẢNH BÁO RỦI RO T+0\n");
        sb.Append(
            $"Đã chạm ngưỡng bảo vệ (rút từ đỉnh {SignedPct(-Math.Abs(drawdownFromAnchor))}, " +
            $"P&amp;L so entry {SignedPct(currentGain)})");
        AppendReasoning(sb, reasoning);
        sb.Append($"\nVol: {VolM(row.SessionVolume)}");
        sb.Append("\nChưa đủ T+2.5 — chỉ theo dõi, chưa bán được.");
        return sb.ToString();
    }

    public static string FormatMaster(
        DailyOpportunityRecord opp,
        EntryPointDto? entry,
        KbsPriceBoardClient.KbsBoardRow row,
        string signalKey,
        MasterAlertSessionTracker.SymbolMasterState state,
        MasterAlertOptions cfg,
        string? reasoning = null,
        string? buyTriggerBranch = null) => signalKey switch
    {
        MasterAlertKinds.BuyPoint1 => FormatBuyPoint1(opp, entry, row, cfg.SlippageBufferPercent, reasoning, buyTriggerBranch),
        MasterAlertKinds.BuyPoint2 => FormatBuyPoint2(opp, entry, row, cfg.SlippageBufferPercent, reasoning, buyTriggerBranch),
        MasterAlertKinds.CutLoss1 => FormatCutLoss1(opp, row, state, reasoning),
        MasterAlertKinds.CutAll => FormatCutAll(opp, row, state, reasoning),
        MasterAlertKinds.SellPoint1Half => FormatSellHalf(opp.Symbol, state.PeakGainPercent(), 0m, row, reasoning),
        MasterAlertKinds.SellAll => FormatSellAll(opp.Symbol, state.PeakGainPercent(), 0m, row, reasoning),
        MasterAlertKinds.RiskWarningIntraday => FormatRiskWarning(opp.Symbol, 0m, 0m, row, reasoning),
        _ => FormatBuyPoint1(opp, entry, row, cfg.SlippageBufferPercent, reasoning, buyTriggerBranch),
    };

    internal static string F(decimal value) =>
        value.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>+ lãi / − lỗ / 0% — luôn có dấu rõ ràng.</summary>
    internal static string SignedPct(decimal pct)
    {
        var abs = Math.Abs(pct).ToString("0.#", CultureInfo.InvariantCulture);
        if (pct > 0) return $"+{abs}%";
        if (pct < 0) return $"-{abs}%";
        return "0%";
    }

    private static void AppendSlippageBuffer(
        StringBuilder sb,
        EntryPointDto? entry,
        decimal slippageBufferPercent)
    {
        if (entry?.BaseHigh > 0 && slippageBufferPercent > 0)
        {
            var maxChasePrice = entry.BaseHigh * (1 + slippageBufferPercent / 100m);
            sb.Append(
                $"\n⚠️ Giá đuổi tối đa: <code>{F(maxChasePrice)}</code> " +
                $"({slippageBufferPercent.ToString("0.#", CultureInfo.InvariantCulture)}% slippage)");
        }
    }

    private static void AppendReasoning(StringBuilder sb, string? reasoning)
    {
        if (string.IsNullOrWhiteSpace(reasoning))
            return;

        sb.Append('\n');
        sb.Append(reasoning);
    }

    private static string VolM(long volume)
    {
        var m = volume / 1_000_000m;
        return m.ToString("0.#", CultureInfo.InvariantCulture) + "M";
    }
}
