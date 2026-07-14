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

/// <summary>Telegram VIP — Master alerts + Entry zone cho Top cơ hội; bán settlement-aware qua vị thế SQL.</summary>
internal sealed class TopOpportunityVipAlertPublisher(
    IDailyOpportunityRepository opportunities,
    ISetupTrackRepository setupTracks,
    IMasterAlertPositionRepository positions,
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
            MarketPhase: "Favorable",
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
        var sellRow = FakeRow("GAS", 95.5m, 99.5m, 95.0m, 3.5m, 1_100_000);
        var sellAllRow = FakeRow("GAS", 93.5m, 99.5m, 93.0m, 1.0m, 1_100_000);
        var warnRow = FakeRow("GAS", 96.0m, 99.5m, 95.5m, 2.0m, 900_000);

        var scenarios = new (string Key, string Body)[]
        {
            (TopOpportunityVipAlertEvaluator.EntryReadySignal,
                VipTelegramMessageFormatter.FormatEntryReady(opp, entry, entryRow)),
            (MasterAlertKinds.BuyPoint1,
                VipTelegramMessageFormatter.FormatBuyPoint1(opp, entry, buy1Row, masterOptions.Value.SlippageBufferPercent)),
            (MasterAlertKinds.BuyPoint2,
                VipTelegramMessageFormatter.FormatBuyPoint2(opp, entry, buy2Row, masterOptions.Value.SlippageBufferPercent)),
            (MasterAlertKinds.RiskWarningIntraday,
                VipTelegramMessageFormatter.FormatRiskWarning("GAS", 4.2m, 0.8m, warnRow)),
            (MasterAlertKinds.SellPoint1Half,
                VipTelegramMessageFormatter.FormatSellHalf("GAS", 4.1m, 1.0m, sellRow)),
            (MasterAlertKinds.SellAll,
                VipTelegramMessageFormatter.FormatSellAll("GAS", 4.1m, -1.5m, sellAllRow)),
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

    public async Task<IReadOnlyDictionary<string, MasterAlertPositionRecord>> LoadOpenPositionMapAsync(
        CancellationToken cancellationToken)
    {
        var rows = await positions.GetOpenPositionsAsync(cancellationToken);
        if (rows.Count == 0)
            return new Dictionary<string, MasterAlertPositionRecord>(StringComparer.OrdinalIgnoreCase);

        return rows.ToDictionary(r => r.Symbol, r => r, StringComparer.OrdinalIgnoreCase);
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
        await HydrateBuyStateFromSqlAsync(opp.Symbol, state, cancellationToken);

        var entry = EntryPointJsonMapper.FromJson(opp.EntryPointJson);
        if (entry is not null
            && entry.IsActionable
            && !state.EntryReadyFired
            && !state.BuyPoint1Fired
            && TopOpportunityVipAlertEvaluator.IsPriceInEntryZone(entry, row.Close))
        {
            var entryReasoning = BuildEntryReadyReasoning(entry);
            await DispatchAsync(
                opp.Symbol,
                opp.VolumeRatio,
                TopOpportunityVipAlertEvaluator.EntryReadySignal,
                VipTelegramMessageFormatter.FormatEntryReady(opp, entry, row, entryReasoning),
                row.Close,
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
            elapsedFraction,
            masterCfg.MinElapsedFractionForPacing);

        var marketPhase = string.IsNullOrWhiteSpace(opp.MarketPhase) ? "Neutral" : opp.MarketPhase;
        var masterSignal = TopOpportunityVipAlertEvaluator.EvaluateMasterSignal(
            masterCfg, state, entry, row, scan, pacedVolumeRatio, opp.AverageDailyVolume, marketPhase);
        if (masterSignal is null)
            return;

        // Guard: vị thế SQL đã có signal này (survive API restart / multi-instance) → bỏ qua
        var existingPosition = await positions.GetOpenBySymbolAsync(opp.Symbol, cancellationToken);
        if (existingPosition is not null
            && existingPosition.FiredAlertKinds.Contains(masterSignal, StringComparer.Ordinal))
        {
            ApplyBuyKindsToState(state, existingPosition);
            return;
        }

        if (!cooldown.ShouldSend(opp.Symbol, masterSignal, Cooldown(masterCfg)))
            return;

        var reasoning = BuildBuySignalReasoning(opp, row, entry, pacedVolumeRatio);
        await DispatchAsync(
            opp.Symbol,
            opp.VolumeRatio,
            masterSignal,
            VipTelegramMessageFormatter.FormatMaster(opp, entry, row, masterSignal, state, masterCfg, reasoning),
            row.Close,
            sessionDate,
            cancellationToken);

        if (!MasterAlertKinds.IsBuyKind(masterSignal))
            return;

        var size = masterSignal == MasterAlertKinds.BuyPoint2 ? 1.0m : 0.5m;
        await positions.UpsertOnBuyAsync(
            opp.Symbol,
            sessionDate,
            row.Close,
            size,
            masterSignal,
            marketPhase,
            cancellationToken);
        await RegisterMasterTrackAsync(opp, row, masterSignal, sessionDate, cancellationToken);
    }

    public async Task ProcessPositionAsync(
        MasterAlertPositionRecord position,
        KbsPriceBoardClient.KbsBoardRow row,
        TradeEventDetector.DetectedTradeEvent? scan,
        DateOnly sessionDate,
        string marketPhase,
        CancellationToken cancellationToken)
    {
        var masterCfg = masterOptions.Value;
        var tgCfg = telegramOptions.Value;
        if (!tgCfg.Enabled || !tgCfg.VipAlertsEnabled || !masterCfg.Enabled)
            return;

        var newPeak = Math.Max(position.PeakPriceSinceEntry, row.High);
        var phase = string.IsNullOrWhiteSpace(marketPhase) ? "Neutral" : marketPhase;
        var signal = TopOpportunityVipAlertEvaluator.EvaluatePositionSignal(
            masterCfg, position, row, scan, sessionDate, phase);

        if (signal is null)
        {
            if (newPeak > position.PeakPriceSinceEntry)
            {
                await positions.UpdateAsync(
                    position.Id,
                    newPeak,
                    position.CurrentPositionSize,
                    null,
                    cancellationToken);
            }

            return;
        }

        if (!cooldown.ShouldSend(position.Symbol, signal, Cooldown(masterCfg)))
        {
            if (newPeak > position.PeakPriceSinceEntry)
            {
                await positions.UpdateAsync(
                    position.Id,
                    newPeak,
                    position.CurrentPositionSize,
                    null,
                    cancellationToken);
            }

            return;
        }

        var peakGain = position.EntryPrice > 0
            ? Math.Round((newPeak - position.EntryPrice) / position.EntryPrice * 100m, 1)
            : 0m;
        var currentGain = position.EntryPrice > 0
            ? Math.Round((row.Close - position.EntryPrice) / position.EntryPrice * 100m, 1)
            : 0m;
        var drawdown = Math.Max(0m, peakGain - currentGain);
        var reasoning = BuildPositionSignalReasoning(signal, peakGain, currentGain, drawdown, phase, scan, masterCfg);

        var body = signal switch
        {
            MasterAlertKinds.RiskWarningIntraday =>
                VipTelegramMessageFormatter.FormatRiskWarning(position.Symbol, drawdown, currentGain, row, reasoning),
            MasterAlertKinds.SellPoint1Half =>
                VipTelegramMessageFormatter.FormatSellHalf(position.Symbol, peakGain, currentGain, row, reasoning),
            MasterAlertKinds.SellAll =>
                VipTelegramMessageFormatter.FormatSellAll(position.Symbol, peakGain, currentGain, row, reasoning),
            _ => VipTelegramMessageFormatter.FormatRiskWarning(position.Symbol, drawdown, currentGain, row, reasoning),
        };

        await DispatchAsync(
            position.Symbol,
            0m,
            signal,
            body,
            row.Close,
            sessionDate,
            cancellationToken);

        if (MasterAlertKinds.IsRiskWarning(signal))
        {
            await positions.UpdateAsync(
                position.Id,
                newPeak,
                position.CurrentPositionSize,
                signal,
                cancellationToken);
            return;
        }

        if (signal == MasterAlertKinds.SellPoint1Half)
        {
            await positions.UpdateAsync(
                position.Id,
                newPeak,
                0.5m,
                signal,
                cancellationToken);
            return;
        }

        if (signal == MasterAlertKinds.SellAll)
            await positions.CloseAsync(position.Id, sessionDate, signal, cancellationToken);
    }

    private async Task HydrateBuyStateFromSqlAsync(
        string symbol,
        MasterAlertSessionTracker.SymbolMasterState state,
        CancellationToken cancellationToken)
    {
        if (state.SqlHydrated)
            return;

        state.SqlHydrated = true;
        var existing = await positions.GetOpenBySymbolAsync(symbol, cancellationToken);
        if (existing is not null)
            ApplyBuyKindsToState(state, existing);
    }

    private static void ApplyBuyKindsToState(
        MasterAlertSessionTracker.SymbolMasterState state,
        MasterAlertPositionRecord position)
    {
        var hasBuy1 = position.FiredAlertKinds.Contains(MasterAlertKinds.BuyPoint1, StringComparer.Ordinal);
        var hasBuy2 = position.FiredAlertKinds.Contains(MasterAlertKinds.BuyPoint2, StringComparer.Ordinal);
        if (!hasBuy1 && !hasBuy2)
            return;

        state.BuyPoint1Fired = true;
        state.EntryReadyFired = true;
        if (state.BuyPoint1Price <= 0)
            state.BuyPoint1Price = position.EntryPrice;
        if (state.SessionHighSinceBuy1 < position.PeakPriceSinceEntry)
            state.SessionHighSinceBuy1 = position.PeakPriceSinceEntry;
        if (hasBuy2)
            state.BuyPoint2Fired = true;
    }

    private static TimeSpan Cooldown(MasterAlertOptions cfg) =>
        TimeSpan.FromMinutes(Math.Max(1, cfg.CooldownMinutes));

    private static string BuildEntryReadyReasoning(EntryPointDto entry)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(entry.Headline))
            parts.Add(entry.Headline);
        if (!string.IsNullOrWhiteSpace(entry.Action))
            parts.Add(entry.Action);
        return string.Join("\n", parts);
    }

    private static string BuildBuySignalReasoning(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        EntryPointDto? entry,
        decimal pacedVolumeRatio)
    {
        var parts = new List<string>();
        var gainFromBase = TopOpportunityVipAlertEvaluator.GainFromBasePeakPercent(entry, row.Close);
        if (entry?.BaseHigh > 0)
        {
            parts.Add(
                $"Phá nền {SignedPlus(gainFromBase)} " +
                $"({VipTelegramMessageFormatter.F(entry.BaseHigh)} → {VipTelegramMessageFormatter.F(row.Close)})");
        }

        if (pacedVolumeRatio >= 1.0m)
            parts.Add($"Vol: {pacedVolumeRatio:0.0}x TB (paced)");
        else if (pacedVolumeRatio > 0)
            parts.Add($"Vol paced: {pacedVolumeRatio:0.0}x TB");

        if (!string.IsNullOrWhiteSpace(opp.MarketPhase))
            parts.Add($"Phase: {opp.MarketPhase}");

        return string.Join("\n", parts);
    }

    private static string BuildPositionSignalReasoning(
        string signal,
        decimal peakGain,
        decimal currentGain,
        decimal drawdown,
        string marketPhase,
        TradeEventDetector.DetectedTradeEvent? scan,
        MasterAlertOptions cfg)
    {
        var parts = new List<string>();

        if (!cfg.MarketPhaseMultipliers.TryGetValue(marketPhase, out var multiplier))
            multiplier = 1.0m;

        var dynamicStop1 = cfg.BaseTrailingStopPercent1 * multiplier;
        var dynamicStop2 = cfg.BaseTrailingStopPercent2 * multiplier;

        if (signal == MasterAlertKinds.RiskWarningIntraday)
        {
            if (TopOpportunityVipAlertEvaluator.IsDistributionScan(scan))
                parts.Add("Phân phối: " + GetDistributionLabel(scan));
            else
                parts.Add($"Drawdown từ peak ≥ {cfg.RiskWarningDrawdownFromPeakPercent:0.#}%");
            return string.Join("\n", parts);
        }

        var isTrailingStop = peakGain >= cfg.TrailingStopMinPeak
            && ((signal == MasterAlertKinds.SellPoint1Half && drawdown >= dynamicStop1)
                || (signal == MasterAlertKinds.SellAll && drawdown >= dynamicStop2));

        if (isTrailingStop)
        {
            parts.Add($"Trailing stop: Mất {drawdown:0.0}% lãi từ peak");
            parts.Add($"(Peak {SignedPlus(peakGain)} → hiện {SignedPlus(currentGain)})");
            var stopPct = signal == MasterAlertKinds.SellPoint1Half ? dynamicStop1 : dynamicStop2;
            parts.Add($"Phase: {marketPhase} (stop {stopPct:0.0}%)");
        }
        else
        {
            parts.Add("Phân phối: " + GetDistributionLabel(scan));
            parts.Add($"Peak {SignedPlus(peakGain)}");
        }

        return string.Join("\n", parts);
    }

    private static string GetDistributionLabel(TradeEventDetector.DetectedTradeEvent? scan)
    {
        if (scan is null)
            return "Lô lớn bán";

        if (string.Equals(scan.Label, TradeEventLabels.Xa, StringComparison.Ordinal))
            return "Lô lớn XẢ";

        if (scan.ForeignNetDelta < 0 && scan.PropDelta <= 0)
            return "Ngoại + Tự doanh bán";

        return "Áp lực bán";
    }

    private static string SignedPlus(decimal pct) =>
        "+" + Math.Abs(pct).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private async Task DispatchAsync(
        string symbol,
        decimal volumeRatio,
        string signalKey,
        string telegramBody,
        decimal price,
        DateOnly sessionDate,
        CancellationToken cancellationToken)
    {
        var title = signalKey switch
        {
            TopOpportunityVipAlertEvaluator.EntryReadySignal => $"{symbol} — Entry ready",
            _ => $"{symbol} — {MasterAlertKinds.Label(signalKey)}",
        };

        var alert = new Alert(
            Guid.NewGuid(),
            symbol,
            TopOpportunityVipAlertEvaluator.SignalTypeFor(signalKey),
            title,
            telegramBody,
            DateTime.UtcNow,
            TopOpportunityVipAlertEvaluator.CategoryFor(signalKey),
            volumeRatio,
            null,
            AlertService.MasterAlertSource);

        await alerts.AddAsync(alert, cancellationToken);
        await publisher.PublishAlertAsync(DtoMapper.ToDto(alert), cancellationToken);
        await telegram.SendAsync(telegramBody, cancellationToken, TelegramNotifier.HtmlParseMode);

        logger.LogInformation(
            "VIP Telegram {Signal} {Symbol} @ {Price} phiên {Date}",
            signalKey,
            symbol,
            price,
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
