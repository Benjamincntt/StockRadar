using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Mapping;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>Telegram VIP — Master alerts + Entry zone cho Top cơ hội trong phiên.</summary>
internal sealed class TopOpportunityVipAlertPublisher(
    IDailyOpportunityRepository opportunities,
    ISetupTrackRepository setupTracks,
    IAlertRepository alerts,
    IMarketRealtimePublisher publisher,
    ITelegramNotifier telegram,
    MasterAlertSessionTracker masterState,
    IntradayAlertTracker cooldown,
    IOptions<MasterAlertOptions> masterOptions,
    IOptions<TelegramNotifyOptions> telegramOptions,
    ILogger<TopOpportunityVipAlertPublisher> logger)
{
    public async Task<IReadOnlyDictionary<string, DailyOpportunityRecord>> LoadTodayTopMapAsync(
        CancellationToken cancellationToken)
    {
        var sessionDate = VietnamMarketCalendar.TodayVietnam();
        var rows = await opportunities.GetForDateAsync(sessionDate, cancellationToken);
        if (rows.Count == 0)
            return new Dictionary<string, DailyOpportunityRecord>(StringComparer.OrdinalIgnoreCase);

        return rows
            .OrderBy(r => r.Rank)
            .ToDictionary(r => r.Symbol, r => r, StringComparer.OrdinalIgnoreCase);
    }

    public async Task ProcessQuoteAsync(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        TradeEventDetector.DetectedTradeEvent? scan,
        DateOnly sessionDate,
        CancellationToken cancellationToken)
    {
        var masterCfg = masterOptions.Value;
        var tgCfg = telegramOptions.Value;
        if (!tgCfg.Enabled || !tgCfg.VipAlertsEnabled)
            return;

        var entry = EntryPointJsonMapper.FromJson(opp.EntryPointJson);
        if (entry is not null
            && TopOpportunityVipAlertEvaluator.IsPriceInEntryZone(entry, row.Close)
            && cooldown.ShouldSend(opp.Symbol, TopOpportunityVipAlertEvaluator.EntryReadySignal, Cooldown(masterCfg)))
        {
            await DispatchAsync(
                opp,
                row,
                TopOpportunityVipAlertEvaluator.EntryReadySignal,
                FormatEntryReady(opp, entry, row),
                sessionDate,
                cancellationToken);
        }

        if (!masterCfg.Enabled)
            return;

        var state = masterState.GetOrReset(opp.Symbol, sessionDate);
        var masterSignal = TopOpportunityVipAlertEvaluator.EvaluateMasterSignal(masterCfg, state, row, scan);
        if (masterSignal is null)
            return;

        if (!cooldown.ShouldSend(opp.Symbol, masterSignal, Cooldown(masterCfg)))
            return;

        await DispatchAsync(
            opp,
            row,
            masterSignal,
            FormatMaster(opp, row, masterSignal, state),
            sessionDate,
            cancellationToken);

        if (MasterAlertKinds.IsBuyKind(masterSignal))
            await RegisterMasterTrackAsync(opp, row, masterSignal, sessionDate, cancellationToken);
    }

    private static TimeSpan Cooldown(MasterAlertOptions cfg) =>
        TimeSpan.FromMinutes(Math.Max(1, cfg.CooldownMinutes));

    private async Task DispatchAsync(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        string signalKey,
        string telegramBody,
        DateOnly sessionDate,
        CancellationToken cancellationToken)
    {
        var title = signalKey switch
        {
            TopOpportunityVipAlertEvaluator.EntryReadySignal => $"{opp.Symbol} — Entry ready",
            _ => $"{opp.Symbol} — {MasterAlertKinds.Label(signalKey)}",
        };

        var alert = new Alert(
            Guid.NewGuid(),
            opp.Symbol,
            TopOpportunityVipAlertEvaluator.SignalTypeFor(signalKey),
            title,
            telegramBody,
            DateTime.UtcNow,
            TopOpportunityVipAlertEvaluator.CategoryFor(signalKey),
            opp.VolumeRatio,
            null,
            AlertService.MasterAlertSource);

        await alerts.AddAsync(alert, cancellationToken);
        await publisher.PublishAlertAsync(DtoMapper.ToDto(alert), cancellationToken);
        await telegram.SendAsync(telegramBody, cancellationToken);

        logger.LogInformation(
            "VIP Telegram {Signal} {Symbol} @ {Price} phiên {Date}",
            signalKey,
            opp.Symbol,
            row.Close,
            sessionDate);
    }

    private async Task RegisterMasterTrackAsync(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        string sourceType,
        DateOnly sessionDate,
        CancellationToken cancellationToken)
    {
        var exists = await setupTracks.ExistsAsync(
            opp.Symbol,
            sourceType,
            sessionDate,
            cancellationToken);

        if (exists)
            return;

        await setupTracks.AddAsync(
            new SetupTrackRecord(
                Guid.NewGuid(),
                opp.Symbol,
                sourceType,
                sessionDate,
                row.Close,
                sessionDate,
                opp.Rank,
                opp.Score,
                row.ChangePercent,
                row.SessionVolume,
                null,
                false,
                null,
                null,
                null,
                null,
                sessionDate,
                opp.PredictedHitPercent,
                opp.SetupDna,
                opp.ExplainJson,
                TradeState: opp.TradeState,
                TradeStateReason: opp.TradeStateReason),
            cancellationToken);
    }

    private static string FormatEntryReady(
        DailyOpportunityRecord opp,
        EntryPointDto entry,
        KbsPriceBoardClient.KbsBoardRow row)
    {
        var low = Math.Min(entry.BaseLow, entry.EntryPrice);
        var high = Math.Max(entry.EntryPrice, entry.TriggerPrice);
        var ci = CultureInfo.InvariantCulture;

        return
            $"🎯 [ENTRY READY] {opp.Symbol}\n" +
            $"Giá: {row.Close.ToString("N1", ci)} ({row.ChangePercent.ToString("+0.##;-0.##", ci)}%)\n" +
            $"Vùng mua AI: {low.ToString("N1", ci)} – {high.ToString("N1", ci)}\n" +
            $"Loại: {entry.Type} · {entry.Headline}\n" +
            $"Top #{opp.Rank} · Buy {opp.BuyScore} · {opp.SetupDna ?? "—"}";
    }

    private static string FormatMaster(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        string signalKey,
        MasterAlertSessionTracker.SymbolMasterState state)
    {
        var ci = CultureInfo.InvariantCulture;
        var label = MasterAlertKinds.Label(signalKey);
        var emoji = MasterAlertKinds.IsSellKind(signalKey) ? "🛑" : "🚀";
        var volM = row.SessionVolume / 1_000_000m;

        var lines =
            $"{emoji} [{label.ToUpperInvariant()}] {opp.Symbol}\n" +
            $"Giá: {row.Close.ToString("N1", ci)} · Phiên {row.ChangePercent.ToString("+0.##;-0.##", ci)}%\n" +
            $"KL: {volM.ToString("0.##", ci)}M\n" +
            $"Top #{opp.Rank} · Buy {opp.BuyScore} · {opp.SetupDna ?? "—"}";

        if (state.BuyPoint1Fired && state.BuyPoint1Price > 0)
        {
            lines += $"\nĐỉnh từ M1: +{state.PeakGainPercent().ToString("0.##", ci)}%";
        }

        return lines;
    }
}
