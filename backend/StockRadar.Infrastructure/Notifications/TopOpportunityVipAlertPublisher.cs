using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Mapping;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
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
    ILogger<TopOpportunityVipAlertPublisher> logger) : IVipTelegramAlertTestService
{
    public async Task<VipTelegramTestResultDto> SendSampleAlertsAsync(CancellationToken cancellationToken = default)
    {
        var tgCfg = telegramOptions.Value;
        if (!tgCfg.Enabled)
            return new VipTelegramTestResultDto(0, [], "TelegramNotify.Enabled = false");

        if (string.IsNullOrWhiteSpace(tgCfg.BotToken) || string.IsNullOrWhiteSpace(tgCfg.ChatId))
            return new VipTelegramTestResultDto(0, [], "BotToken hoặc ChatId trống");

        var opp = new DailyOpportunityRecord(
            VietnamMarketCalendar.TodayVietnam(),
            Rank: 3,
            Symbol: "GAS",
            Name: "PV Gas",
            Sector: "Dầu khí",
            Score: 82,
            Price: 97.2m,
            ChangePercent: 4.2m,
            VolumeRatio: 1.8m,
            GeneratedAt: DateTime.UtcNow,
            BuyScore: 78,
            PredictedHitPercent: 42m,
            SetupDna: "Breakout+RS",
            TradeState: "Actionable",
            TradeStateReason: "Xác nhận Breakout + RS",
            AverageDailyVolume: 1_200_000,
            EntryPointJson: EntryPointJsonMapper.ToJson(new EntryPointDto(
                Status: nameof(EntryPointStatus.Ready),
                Type: nameof(EntryPointType.Breakout),
                Confidence: 75,
                EntryPrice: 97.0m,
                StopLoss: 95.0m,
                TriggerPrice: 97.5m,
                TargetPrice: 102.0m,
                BaseLow: 96.0m,
                BaseHigh: 97.0m,
                GainFromBasePercent: 4.2m,
                RiskRewardRatio: 2.1m,
                IsActionable: true,
                Headline: "RS mạnh",
                Action: "Mua vùng trigger",
                Checklist: [])));

        var entryRow = FakeRow("GAS", 97.2m, 97.5m, 96.8m, 1.5m, 520_000);
        var entry = EntryPointJsonMapper.FromJson(opp.EntryPointJson)!;

        var buy1Row = FakeRow("GAS", 100.4m, 100.6m, 99.5m, 2.5m, 1_200_000);
        var buy2Row = FakeRow("GAS", 102.9m, 103.2m, 101.0m, 5.5m, 1_450_000);

        var cutState = new MasterAlertSessionTracker.SymbolMasterState(VietnamMarketCalendar.TodayVietnam())
        {
            BuyPoint1Fired = true,
            BuyPoint1Price = 95.28m,
            SessionHighSinceBuy1 = 99.5m,
        };
        var cutRow = FakeRow("GAS", 95.5m, 99.5m, 95.0m, 3.5m, 1_100_000);
        var cutAllRow = FakeRow("GAS", 93.5m, 99.5m, 93.0m, 1.0m, 1_100_000);

        var scenarios = new (string Key, string Body)[]
        {
            (TopOpportunityVipAlertEvaluator.EntryReadySignal,
                VipTelegramMessageFormatter.FormatEntryReady(opp, entry, entryRow)),
            (MasterAlertKinds.BuyPoint1,
                VipTelegramMessageFormatter.FormatBuyPoint1(opp, entry, buy1Row)),
            (MasterAlertKinds.BuyPoint2,
                VipTelegramMessageFormatter.FormatBuyPoint2(opp, entry, buy2Row)),
            (MasterAlertKinds.CutLoss1,
                VipTelegramMessageFormatter.FormatCutLoss1(opp, cutRow, cutState)),
            (MasterAlertKinds.CutAll,
                VipTelegramMessageFormatter.FormatCutAll(opp, cutAllRow, cutState)),
        };

        var sent = new List<string>();
        foreach (var (key, body) in scenarios)
        {
            await telegram.SendAsync(body, cancellationToken, TelegramNotifier.HtmlParseMode);
            sent.Add(key);
            await Task.Delay(400, cancellationToken);
        }

        logger.LogInformation("VIP Telegram test: đã gửi {Count} mẫu.", sent.Count);
        return new VipTelegramTestResultDto(sent.Count, sent);
    }

    private static KbsPriceBoardClient.KbsBoardRow FakeRow(
        string symbol,
        decimal close,
        decimal high,
        decimal low,
        decimal changePct,
        long volume) =>
        new(
            symbol,
            Open: close * 0.98m,
            High: high,
            Low: low,
            Close: close,
            SessionVolume: volume,
            ChangePercent: changePct,
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

        var state = masterState.GetOrReset(opp.Symbol, sessionDate);

        var entry = EntryPointJsonMapper.FromJson(opp.EntryPointJson);
        if (entry is not null
            && entry.IsActionable
            && !state.EntryReadyFired
            && !state.BuyPoint1Fired
            && TopOpportunityVipAlertEvaluator.IsPriceInEntryZone(entry, row.Close))
        {
            await DispatchAsync(
                opp,
                row,
                TopOpportunityVipAlertEvaluator.EntryReadySignal,
                VipTelegramMessageFormatter.FormatEntryReady(opp, entry, row),
                sessionDate,
                cancellationToken);
            state.EntryReadyFired = true;
        }

        if (!masterCfg.Enabled)
            return;

        var elapsedFraction = VietnamMarketCalendar.SessionElapsedFraction();
        var pacedVolumeRatio = TopOpportunityVipAlertEvaluator.ComputePacedVolumeRatio(
            row.SessionVolume,
            opp.AverageDailyVolume,
            elapsedFraction);

        var masterSignal = TopOpportunityVipAlertEvaluator.EvaluateMasterSignal(
            masterCfg, state, entry, row, scan, pacedVolumeRatio, opp.AverageDailyVolume);
        if (masterSignal is null)
            return;

        if (!cooldown.ShouldSend(opp.Symbol, masterSignal, Cooldown(masterCfg)))
            return;

        await DispatchAsync(
            opp,
            row,
            masterSignal,
            VipTelegramMessageFormatter.FormatMaster(opp, entry, row, masterSignal, state, masterCfg),
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
        await telegram.SendAsync(telegramBody, cancellationToken, TelegramNotifier.HtmlParseMode);

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
}
