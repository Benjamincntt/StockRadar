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
using StockRadar.Domain.Services;
using StockRadar.Domain.Services.OpportunityRanking;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>Telegram VIP — Master alerts + Entry zone cho Top cơ hội; bán settlement-aware qua vị thế SQL.</summary>
internal sealed class TopOpportunityVipAlertPublisher(
    IDailyOpportunityRepository opportunities,
    ISetupTrackRepository setupTracks,
    IMasterAlertPositionRepository positions,
    IVipAlertFireRepository vipFires,
    IAlertRepository alerts,
    IMarketRealtimePublisher publisher,
    ITelegramNotifier telegram,
    MasterAlertSessionTracker masterState,
    IntradayAlertTracker cooldown,
    VipPullbackMaCache pullbackMaCache,
    VipPositionHistoryCache positionHistoryCache,
    SessionFlowTracker sessionFlow,
    IOpportunityRanker opportunityRanker,
    IVipIntradayRanker vipIntradayRanker,
    IVipIntradayCalibrationService vipIntradayCalibration,
    IVipIntradayThresholdService vipIntradayThresholds,
    IVipLlmJudge vipLlmJudge,
    VipLlmContextBuilder vipLlmContext,
    IOptions<VipLlmJudgeOptions> vipLlmOptions,
    IJobStockRepository stocks,
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
                VipTelegramMessageFormatter.FormatRiskWarning("GAS", 4.2m, 0.8m, warnRow,
                    "Chế độ: BlueSky\nRút từ đỉnh -4.2% so mốc 100\nP&L so entry +0.8%")),
            (MasterAlertKinds.SellPoint1Half + "_BlueSky",
                VipTelegramMessageFormatter.FormatSellHalf("GAS", 4.1m, 1.0m, sellRow,
                    "Chế độ: BlueSky\nRút từ đỉnh -4.0% so mốc 100\nP&L so entry +1%\nPhase: Neutral (ngưỡng 4.0%)")),
            (MasterAlertKinds.SellPoint1Half + "_UnderBase",
                VipTelegramMessageFormatter.FormatSellHalf("GAS", 8.0m, 5.0m, sellRow,
                    "Chế độ: UnderBase\nMục tiêu cạnh dưới nền 10–12\nP&L so entry +5% (peak +8%)")),
            (MasterAlertKinds.SellAll,
                VipTelegramMessageFormatter.FormatSellAll("GAS", 4.1m, -1.5m, sellAllRow,
                    "Chế độ: BlueSky\nRút từ đỉnh -6.0% so mốc 100\nP&L so entry -1.5%\nPhase: Neutral (ngưỡng 6.0%)")),
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

    public async Task PrefetchPullbackMaAsync(
        IEnumerable<string> symbols,
        DateOnly sessionDate,
        CancellationToken cancellationToken = default) =>
        await pullbackMaCache.PrefetchAsync(symbols, sessionDate, stocks, cancellationToken);

    public async Task PrefetchPositionHistoryAsync(
        IEnumerable<string> symbols,
        DateOnly sessionDate,
        CancellationToken cancellationToken = default) =>
        await positionHistoryCache.PrefetchAsync(symbols, sessionDate, stocks, cancellationToken);

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
        if (!state.EntryReadyFired
            && await vipFires.HasFiredAsync(
                opp.Symbol,
                TopOpportunityVipAlertEvaluator.EntryReadySignal,
                sessionDate,
                cancellationToken))
        {
            state.EntryReadyFired = true;
        }

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
            await RecordEntryReadyFireAsync(opp, row, sessionDate, cancellationToken);
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
        var pullbackMa = pullbackMaCache.Get(opp.Symbol);
        var flow = sessionFlow.Get(opp.Symbol);
        var (mlProb, mlActive, featuresComplete, rs5d, atrPct, distMa20) =
            BuildMlSnapshot(opp, row, pullbackMa, marketPhase, flow, scan);
        var resolvedMin = vipIntradayThresholds.ResolveMinMlProb(marketPhase);

        var masterSignal = TopOpportunityVipAlertEvaluator.EvaluateMasterSignal(
            masterCfg,
            state,
            entry,
            row,
            scan,
            pacedVolumeRatio,
            opp.AverageDailyVolume,
            marketPhase,
            pullbackMa,
            mlProb,
            mlActive,
            featuresComplete,
            resolvedMin,
            flow?.SessionForeignNet,
            orderflowObserved: flow is not null,
            out var buyTriggerBranch,
            out var blockedByMl,
            out var blockedByAntiSpam);
        if (blockedByMl)
        {
            logger.LogInformation(
                "VIP rejected_ml {Symbol} P={Prob:0.#} < {Min:0.#} ({Phase})",
                opp.Symbol,
                mlProb,
                resolvedMin,
                marketPhase);
        }

        if (blockedByAntiSpam)
        {
            logger.LogInformation(
                "VIP rejected_antispam {Symbol} P={Prob:0.#} border@{Min:0.#} foreign={Foreign} vsa={Vsa}",
                opp.Symbol,
                mlProb,
                resolvedMin,
                flow?.SessionForeignNet,
                scan?.Label);
        }

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

        VipLlmJudgeResult? llm = null;
        if (MasterAlertKinds.IsBuyKind(masterSignal) && vipLlmJudge.IsEnabled)
        {
            var contextJson = await vipLlmContext.BuildAsync(
                opp,
                row,
                masterSignal,
                buyTriggerBranch,
                pacedVolumeRatio,
                mlProb,
                mlActive,
                resolvedMin,
                pullbackMa,
                flow,
                scan,
                cancellationToken);
            llm = await vipLlmJudge.DecideAsync(
                new VipLlmJudgeRequest(opp.Symbol, masterSignal, buyTriggerBranch, contextJson),
                cancellationToken);

            var shadow = vipLlmOptions.Value.ShadowMode;
            if (llm.IsBlock && !shadow)
            {
                logger.LogInformation(
                    "VIP rejected_llm {Symbol} {Signal} ({Ms}ms): {Reason}",
                    opp.Symbol,
                    masterSignal,
                    llm.LatencyMs,
                    llm.Reason);
                // Ghi fire bị chặn để đo sau (không tạo vị thế / không Telegram).
                await RecordVipFireAsync(
                    opp,
                    row,
                    masterSignal,
                    buyTriggerBranch,
                    pacedVolumeRatio,
                    mlProb,
                    mlActive,
                    featuresComplete,
                    rs5d,
                    atrPct,
                    distMa20,
                    pullbackMa,
                    flow,
                    scan,
                    sessionDate,
                    cancellationToken,
                    llm);
                return;
            }

            if (llm.IsBlock && shadow)
            {
                logger.LogInformation(
                    "VIP shadow_llm_block {Symbol} {Signal} — vẫn bắn Telegram: {Reason}",
                    opp.Symbol,
                    masterSignal,
                    llm.Reason);
            }
        }

        var reasoning = BuildBuySignalReasoning(
            opp, row, entry, pacedVolumeRatio, pullbackMa, buyTriggerBranch, mlProb, mlActive);
        if (llm is not null && !string.IsNullOrWhiteSpace(llm.Reason))
            reasoning = string.IsNullOrWhiteSpace(reasoning)
                ? $"AI: {llm.Decision} — {llm.Reason}"
                : reasoning + $"\nAI: {llm.Decision} — {llm.Reason}";

        await DispatchAsync(
            opp.Symbol,
            opp.VolumeRatio,
            masterSignal,
            VipTelegramMessageFormatter.FormatMaster(
                opp, entry, row, masterSignal, state, masterCfg, reasoning, buyTriggerBranch),
            row.Close,
            sessionDate,
            cancellationToken);

        if (!MasterAlertKinds.IsBuyKind(masterSignal))
            return;

        var size = masterSignal == MasterAlertKinds.BuyPoint2 ? 1.0m : 0.5m;
        var overhead = positionHistoryCache.FindOverheadBox(opp.Symbol, row.Close, sessionDate, masterCfg);
        string exitRegime;
        decimal? baseLow = null;
        decimal? baseHigh = null;
        if (overhead is { HasValidBox: true })
        {
            exitRegime = MasterAlertExitRegimes.UnderBase;
            baseLow = overhead.BoxLow;
            baseHigh = overhead.BoxHigh;
        }
        else
        {
            exitRegime = MasterAlertExitRegimes.BlueSky;
        }

        var positionId = await positions.UpsertOnBuyAsync(
            opp.Symbol,
            sessionDate,
            row.Close,
            size,
            masterSignal,
            marketPhase,
            cancellationToken,
            exitRegime,
            baseLow,
            baseHigh,
            row.Low > 0 ? row.Low : row.Close);

        logger.LogInformation(
            "VIP open position {Symbol} regime={Regime} base={Low}-{High} entryBarLow={LowBar}",
            opp.Symbol,
            exitRegime,
            baseLow,
            baseHigh,
            row.Low);
        await RegisterMasterTrackAsync(opp, row, masterSignal, sessionDate, positionId, cancellationToken);
        await RecordVipFireAsync(
            opp,
            row,
            masterSignal,
            buyTriggerBranch,
            pacedVolumeRatio,
            mlProb,
            mlActive,
            featuresComplete,
            rs5d,
            atrPct,
            distMa20,
            pullbackMa,
            flow,
            scan,
            sessionDate,
            cancellationToken,
            llm);
    }

    public async Task TouchFireRangesAsync(
        string symbol,
        DateOnly sessionDate,
        decimal high,
        decimal low,
        CancellationToken cancellationToken = default) =>
        await vipFires.TouchSessionRangeAsync(symbol, sessionDate, high, low, cancellationToken);

    public async Task<int> MeasureIntradayOutcomesAsync(
        DateOnly sessionDate,
        IReadOnlyDictionary<string, decimal> closesBySymbol,
        CancellationToken cancellationToken = default)
    {
        var pending = await vipFires.GetPendingIntradayAsync(sessionDate, cancellationToken);
        var count = 0;
        foreach (var fire in pending)
        {
            if (!closesBySymbol.TryGetValue(fire.Symbol, out var close) || close <= 0)
                continue;

            await vipFires.MarkIntradayMeasuredAsync(
                fire.Id,
                close,
                fire.SessionHighSinceFire,
                fire.SessionLowSinceFire,
                cancellationToken);
            count++;
        }

        if (count > 0)
            logger.LogInformation("VIP intraday measured {Count} fires for {Date}.", count, sessionDate);
        return count;
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
        position = await EnsureExitRegimeAsync(position, sessionDate, cancellationToken);

        // Chuyển UnderBase → BlueSky khi vượt cạnh trên nền
        if (MasterAlertExitRegimes.IsUnderBase(position.ExitRegime)
            && position.OverheadBaseHigh is > 0
            && row.Close > position.OverheadBaseHigh.Value
            && row.SessionVolume > 0)
        {
            await positions.UpdateExitRegimeAsync(
                position.Id,
                MasterAlertExitRegimes.BlueSky,
                null,
                null,
                sessionDate,
                cancellationToken);
            position = position with
            {
                ExitRegime = MasterAlertExitRegimes.BlueSky,
                OverheadBaseLow = null,
                OverheadBaseHigh = null,
                AnchorWindowStart = sessionDate,
            };
            logger.LogInformation(
                "VIP regime switch {Symbol} UnderBase→BlueSky @ {Price}",
                position.Symbol,
                row.Close);
        }

        var history = positionHistoryCache.GetHistory(position.Symbol);
        var anchorStart = position.AnchorWindowStart ?? position.EntryDate;
        var anchor = VipPositionHistoryCache.ComputeAnchorPrice(
            history,
            anchorStart,
            sessionDate,
            masterCfg.AnchorLookbackSessions,
            row.High);

        var candidate = TopOpportunityVipAlertEvaluator.EvaluatePositionSignal(
            masterCfg, position, row, scan, sessionDate, phase, anchor);

        var state = masterState.GetOrReset(position.Symbol, sessionDate);
        if (candidate is null)
        {
            state.ResetOtherSellConfirms("");
            if (newPeak > position.PeakPriceSinceEntry)
            {
                await positions.UpdatePeakAsync(
                    position.Id,
                    newPeak,
                    null,
                    cancellationToken);
            }

            return;
        }

        state.BumpSellConfirm(candidate);
        state.ResetOtherSellConfirms(candidate);
        if (state.GetSellConfirm(candidate) < Math.Max(1, masterCfg.SellConfirmationTicks))
        {
            if (newPeak > position.PeakPriceSinceEntry)
            {
                await positions.UpdatePeakAsync(
                    position.Id,
                    newPeak,
                    null,
                    cancellationToken);
            }

            return;
        }

        var signal = candidate;
        if (!cooldown.ShouldSend(position.Symbol, signal, Cooldown(masterCfg)))
        {
            if (newPeak > position.PeakPriceSinceEntry)
            {
                await positions.UpdatePeakAsync(
                    position.Id,
                    newPeak,
                    null,
                    cancellationToken);
            }

            return;
        }

        state.ResetSellConfirm(signal);

        var dropFromAnchor = anchor > 0
            ? Math.Round(Math.Max(0m, (anchor - row.Close) / anchor * 100m), 1)
            : 0m;
        var currentGain = position.EntryPrice > 0
            ? Math.Round((row.Close - position.EntryPrice) / position.EntryPrice * 100m, 1)
            : 0m;
        var peakGain = position.EntryPrice > 0
            ? Math.Round((newPeak - position.EntryPrice) / position.EntryPrice * 100m, 1)
            : 0m;

        var reasoning = BuildPositionSignalReasoning(
            signal, position, anchor, dropFromAnchor, currentGain, peakGain, phase, scan, masterCfg);

        VipLlmJudgeResult? llm = null;
        if ((MasterAlertKinds.IsSellKind(signal) || MasterAlertKinds.IsRiskWarning(signal))
            && vipLlmJudge.IsEnabled)
        {
            var contextJson = await vipLlmContext.BuildForPositionAsync(
                position,
                row,
                signal,
                anchor,
                dropFromAnchor,
                currentGain,
                peakGain,
                phase,
                scan,
                cancellationToken);
            llm = await vipLlmJudge.DecideAsync(
                new VipLlmJudgeRequest(position.Symbol, signal, position.ExitRegime, contextJson),
                cancellationToken);

            var shadow = vipLlmOptions.Value.ShadowMode;
            if (llm.IsBlock && !shadow)
            {
                logger.LogInformation(
                    "VIP rejected_llm {Symbol} {Signal} ({Ms}ms): {Reason}",
                    position.Symbol,
                    signal,
                    llm.LatencyMs,
                    llm.Reason);
                await RecordSellFireAsync(
                    position, row, signal, phase, anchor, dropFromAnchor, sessionDate, cancellationToken, llm);
                if (newPeak > position.PeakPriceSinceEntry)
                {
                    await positions.UpdatePeakAsync(
                        position.Id,
                        newPeak,
                        null,
                        cancellationToken);
                }

                return;
            }

            if (llm.IsBlock && shadow)
            {
                logger.LogInformation(
                    "VIP shadow_llm_block {Symbol} {Signal} — vẫn bắn Telegram: {Reason}",
                    position.Symbol,
                    signal,
                    llm.Reason);
            }
        }

        if (llm is not null && !string.IsNullOrWhiteSpace(llm.Reason))
        {
            reasoning = string.IsNullOrWhiteSpace(reasoning)
                ? $"AI: {llm.Decision} — {llm.Reason}"
                : reasoning + $"\nAI: {llm.Decision} — {llm.Reason}";
        }

        var body = signal switch
        {
            MasterAlertKinds.RiskWarningIntraday =>
                VipTelegramMessageFormatter.FormatRiskWarning(
                    position.Symbol, dropFromAnchor, currentGain, row, reasoning),
            MasterAlertKinds.SellPoint1Half =>
                VipTelegramMessageFormatter.FormatSellHalf(
                    position.Symbol, peakGain, currentGain, row, reasoning),
            MasterAlertKinds.SellAll =>
                VipTelegramMessageFormatter.FormatSellAll(
                    position.Symbol, peakGain, currentGain, row, reasoning),
            _ => VipTelegramMessageFormatter.FormatRiskWarning(
                position.Symbol, dropFromAnchor, currentGain, row, reasoning),
        };

        await DispatchAsync(
            position.Symbol,
            0m,
            signal,
            body,
            row.Close,
            sessionDate,
            cancellationToken);

        await RecordSellFireAsync(
            position, row, signal, phase, anchor, dropFromAnchor, sessionDate, cancellationToken, llm);

        if (MasterAlertKinds.IsRiskWarning(signal))
        {
            await positions.UpdatePeakAsync(
                position.Id,
                newPeak,
                signal,
                cancellationToken);
            return;
        }

        if (signal == MasterAlertKinds.SellPoint1Half)
        {
            await positions.UpdatePeakAsync(position.Id, newPeak, null, cancellationToken);
            await positions.RecordSellHalfAsync(
                position.Id,
                sessionDate,
                row.Close,
                DateTime.UtcNow,
                "Fire",
                cancellationToken);
            return;
        }

        if (signal == MasterAlertKinds.SellAll)
        {
            await positions.CloseAsync(
                position.Id,
                sessionDate,
                signal,
                row.Close,
                DateTime.UtcNow,
                "Fire",
                cancellationToken);
        }
    }

    private async Task<MasterAlertPositionRecord> EnsureExitRegimeAsync(
        MasterAlertPositionRecord position,
        DateOnly sessionDate,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(position.ExitRegime))
        {
            if (position.AnchorWindowStart is null)
            {
                await positions.UpdateExitRegimeAsync(
                    position.Id,
                    position.ExitRegime!,
                    position.OverheadBaseLow,
                    position.OverheadBaseHigh,
                    position.EntryDate,
                    cancellationToken);
                return position with { AnchorWindowStart = position.EntryDate };
            }

            return position;
        }

        var overhead = positionHistoryCache.FindOverheadBox(
            position.Symbol, position.EntryPrice, sessionDate, masterOptions.Value);
        string regime;
        decimal? low = null;
        decimal? high = null;
        if (overhead is { HasValidBox: true })
        {
            regime = MasterAlertExitRegimes.UnderBase;
            low = overhead.BoxLow;
            high = overhead.BoxHigh;
        }
        else
        {
            regime = MasterAlertExitRegimes.BlueSky;
        }

        await positions.UpdateExitRegimeAsync(
            position.Id, regime, low, high, position.EntryDate, cancellationToken);

        logger.LogInformation(
            "VIP lazy classify {Symbol} → {Regime} base={Low}-{High}",
            position.Symbol,
            regime,
            low,
            high);

        return position with
        {
            ExitRegime = regime,
            OverheadBaseLow = low,
            OverheadBaseHigh = high,
            AnchorWindowStart = position.EntryDate,
            EntryBarLow = position.EntryBarLow,
        };
    }

    private async Task RecordSellFireAsync(
        MasterAlertPositionRecord position,
        KbsPriceBoardClient.KbsBoardRow row,
        string signal,
        string phase,
        decimal anchor,
        decimal dropFromAnchor,
        DateOnly sessionDate,
        CancellationToken cancellationToken,
        VipLlmJudgeResult? llm = null)
    {
        if (!MasterAlertKinds.IsSellKind(signal) && !MasterAlertKinds.IsRiskWarning(signal))
            return;

        var ctx = System.Text.Json.JsonSerializer.Serialize(new
        {
            regime = position.ExitRegime,
            anchor,
            dropFromAnchor,
            overheadLow = position.OverheadBaseLow,
            overheadHigh = position.OverheadBaseHigh,
            entryBarLow = position.EntryBarLow,
            phase,
            threshold1 = masterOptions.Value.SellPoint1DropFromAnchorPercent,
            threshold2 = masterOptions.Value.SellPoint2DropFromAnchorPercent,
        });

        await vipFires.AddAsync(
            new VipAlertFireRecord(
                Guid.NewGuid(),
                position.Symbol,
                sessionDate,
                DateTime.UtcNow,
                signal,
                position.ExitRegime,
                row.Close,
                row.Open,
                TopOpportunityVipAlertEvaluator.GainFromOpenPercent(row.Open, row.Close),
                0m,
                null,
                false,
                null,
                null,
                phase,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                false,
                null,
                null,
                null,
                row.High,
                row.Low,
                llm?.Decision,
                llm?.Reason,
                llm?.LatencyMs,
                llm?.Model,
                llm is not null && vipLlmOptions.Value.ShadowMode,
                SellContextJson: ctx),
            cancellationToken);
    }

    private async Task RecordEntryReadyFireAsync(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        DateOnly sessionDate,
        CancellationToken cancellationToken)
    {
        var gainFromOpen = TopOpportunityVipAlertEvaluator.GainFromOpenPercent(row.Open, row.Close);
        await vipFires.AddAsync(
            new VipAlertFireRecord(
                Guid.NewGuid(),
                opp.Symbol,
                sessionDate,
                DateTime.UtcNow,
                TopOpportunityVipAlertEvaluator.EntryReadySignal,
                null,
                row.Close,
                row.Open,
                gainFromOpen,
                0m,
                null,
                false,
                opp.BuyScore ?? opp.Score,
                opp.PredictedHitPercent,
                opp.MarketPhase,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                false,
                null,
                null,
                null,
                row.High,
                row.Low),
            cancellationToken);
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
        decimal pacedVolumeRatio,
        VipPullbackMaContext pullbackMa,
        string? buyTriggerBranch,
        decimal mlProb,
        bool mlActive)
    {
        var parts = new List<string>();
        var gainFromOpen = TopOpportunityVipAlertEvaluator.GainFromOpenPercent(row.Open, row.Close);

        if (string.Equals(
                buyTriggerBranch,
                TopOpportunityVipAlertEvaluator.BuyTriggerPullback,
                StringComparison.Ordinal))
        {
            var maLabel = pullbackMa.NearMaLabel(row.Close);
            parts.Add($"Hồi sát {maLabel} trong uptrend dài hạn");
            parts.Add(
                $"P&L phiên {VipTelegramMessageFormatter.SignedPct(gainFromOpen)} " +
                $"({VipTelegramMessageFormatter.F(row.Open)} → {VipTelegramMessageFormatter.F(row.Close)})");
        }
        else
        {
            parts.Add(
                $"P&L phiên {VipTelegramMessageFormatter.SignedPct(gainFromOpen)} " +
                $"({VipTelegramMessageFormatter.F(row.Open)} → {VipTelegramMessageFormatter.F(row.Close)})");
            if (entry?.BaseHigh > 0)
            {
                var gainFromBase = TopOpportunityVipAlertEvaluator.GainFromBasePeakPercent(entry, row.Close);
                parts.Add(
                    $"So đỉnh nền: {VipTelegramMessageFormatter.SignedPct(gainFromBase)} " +
                    $"(BaseHigh {VipTelegramMessageFormatter.F(entry.BaseHigh)})");
            }
        }

        if (mlActive)
            parts.Add($"ML P(hit): {mlProb:0.#}%");

        if (pacedVolumeRatio >= 1.0m)
            parts.Add($"Vol: {pacedVolumeRatio:0.0}x TB (paced)");
        else if (pacedVolumeRatio > 0)
            parts.Add($"Vol paced: {pacedVolumeRatio:0.0}x TB");

        if (!string.IsNullOrWhiteSpace(opp.MarketPhase))
            parts.Add($"Phase: {opp.MarketPhase}");

        return string.Join("\n", parts);
    }

    private (decimal MlProb, bool MlActive, bool FeaturesComplete, decimal? Rs5d, decimal? Atr, decimal? DistMa20)
        BuildMlSnapshot(
            DailyOpportunityRecord opp,
            KbsPriceBoardClient.KbsBoardRow row,
            VipPullbackMaContext pullbackMa,
            string marketPhase,
            SessionFlowSnapshot? flow,
            TradeEventDetector.DetectedTradeEvent? scan)
    {
        var rs5d = pullbackMa.LiveRs5dPercent(row.Close);
        var atr = pullbackMa.LiveAtrPercent(row.Close);
        var dist = pullbackMa.LiveDistMa20Percent(row.Close);
        var featuresComplete = pullbackMa.FeaturesComplete && rs5d.HasValue && atr.HasValue && dist.HasValue;

        Enum.TryParse<StockTradeState>(opp.TradeState, ignoreCase: true, out var tradeState);
        var (_, _, sectorRank) = OpportunityRankFeatures.ParseSetupDna(opp.SetupDna);
        Enum.TryParse<MarketWyckoffPhase>(marketPhase, ignoreCase: true, out var phase);

        var dailyInput = OpportunityRankInput.FromEvaluation(
            opp.BuyScore ?? opp.Score,
            opp.PredictedHitPercent ?? 0m,
            sectorRank > 0 ? sectorRank : 99,
            rs5d ?? 0m,
            opp.VolumeRatio > 0 ? opp.VolumeRatio : 1m,
            string.IsNullOrWhiteSpace(opp.TradeState) ? StockTradeState.AwaitingTrigger : tradeState,
            opp.SetupDna,
            phase,
            atr ?? 0m,
            dist ?? 0m);

        var dailyActive = opportunityRanker.IsModelActive;
        var dailyProb = opportunityRanker.PredictWinProbability(dailyInput);

        var gainFromOpen = TopOpportunityVipAlertEvaluator.GainFromOpenPercent(row.Open, row.Close);
        var intradayInput = new VipIntradayInput(
            gainFromOpen,
            TopOpportunityVipAlertEvaluator.ComputePacedVolumeRatio(
                row.SessionVolume,
                opp.AverageDailyVolume,
                VietnamMarketCalendar.SessionElapsedFraction(),
                masterOptions.Value.MinElapsedFractionForPacing),
            dailyProb,
            atr,
            dist,
            pullbackMa.Available ? pullbackMa.UptrendLong : null,
            flow?.SessionForeignNet,
            flow?.SessionPropNet,
            flow?.SessionPressure,
            string.Equals(scan?.Label, TradeEventLabels.Xa, StringComparison.OrdinalIgnoreCase));

        var intradayActive = vipIntradayRanker.IsModelActive;
        var intradayProb = vipIntradayRanker.PredictWinProbability(intradayInput);

        decimal gateProb;
        bool gateActive;
        if (intradayActive && dailyActive && masterOptions.Value.IntradayEnsembleWithDaily)
        {
            gateProb = Math.Min(dailyProb, intradayProb);
            gateActive = true;
        }
        else if (intradayActive)
        {
            gateProb = intradayProb;
            gateActive = true;
        }
        else
        {
            gateProb = dailyProb;
            gateActive = dailyActive;
        }

        if (masterOptions.Value.IntradayCalibrationEnabled)
            gateProb = vipIntradayCalibration.GetProfile().Apply(gateProb);

        return (gateProb, gateActive, featuresComplete, rs5d, atr, dist);
    }

    private async Task RecordVipFireAsync(
        DailyOpportunityRecord opp,
        KbsPriceBoardClient.KbsBoardRow row,
        string signal,
        string? branch,
        decimal pacedVolumeRatio,
        decimal mlProb,
        bool mlActive,
        bool featuresComplete,
        decimal? rs5d,
        decimal? atrPct,
        decimal? distMa20,
        VipPullbackMaContext pullbackMa,
        SessionFlowSnapshot? flow,
        TradeEventDetector.DetectedTradeEvent? scan,
        DateOnly sessionDate,
        CancellationToken cancellationToken,
        VipLlmJudgeResult? llm = null)
    {
        var gainFromOpen = TopOpportunityVipAlertEvaluator.GainFromOpenPercent(row.Open, row.Close);
        await vipFires.AddAsync(
            new VipAlertFireRecord(
                Guid.NewGuid(),
                opp.Symbol,
                sessionDate,
                DateTime.UtcNow,
                signal,
                branch,
                row.Close,
                row.Open,
                gainFromOpen,
                pacedVolumeRatio,
                mlProb,
                mlActive,
                opp.BuyScore ?? opp.Score,
                opp.PredictedHitPercent,
                opp.MarketPhase,
                rs5d,
                atrPct,
                distMa20,
                pullbackMa.Available ? pullbackMa.Ma10 : null,
                pullbackMa.Available ? pullbackMa.Ma20 : null,
                pullbackMa.Available ? pullbackMa.Ma50 : null,
                pullbackMa.Available ? pullbackMa.UptrendLong : null,
                flow?.SessionForeignNet,
                flow?.SessionPropNet,
                flow?.SessionPressure,
                scan?.Label,
                featuresComplete,
                false,
                null,
                null,
                null,
                row.High,
                row.Low,
                llm?.Decision,
                llm?.Reason,
                llm?.LatencyMs,
                llm?.Model,
                llm is not null && vipLlmOptions.Value.ShadowMode),
            cancellationToken);
    }

    private static string BuildPositionSignalReasoning(
        string signal,
        MasterAlertPositionRecord position,
        decimal anchor,
        decimal dropFromAnchor,
        decimal currentGain,
        decimal peakGain,
        string marketPhase,
        TradeEventDetector.DetectedTradeEvent? scan,
        MasterAlertOptions cfg)
    {
        var parts = new List<string>();

        if (!cfg.MarketPhaseMultipliers.TryGetValue(marketPhase, out var multiplier))
            multiplier = 1.0m;

        var stop1 = cfg.SellPoint1DropFromAnchorPercent * multiplier;
        var stop2 = cfg.SellPoint2DropFromAnchorPercent * multiplier;
        var regime = string.IsNullOrWhiteSpace(position.ExitRegime)
            ? MasterAlertExitRegimes.BlueSky
            : position.ExitRegime!;

        parts.Add($"Chế độ: {regime}");

        if (signal == MasterAlertKinds.RiskWarningIntraday)
        {
            if (TopOpportunityVipAlertEvaluator.IsDistributionScan(scan))
                parts.Add("Phân phối: " + GetDistributionLabel(scan));
            else if (MasterAlertExitRegimes.IsUnderBase(regime) && position.OverheadBaseLow is > 0)
                parts.Add($"Đã chạm vùng mục tiêu nền {VipTelegramMessageFormatter.F(position.OverheadBaseLow.Value)}");
            else
            {
                parts.Add(
                    $"Rút từ đỉnh {VipTelegramMessageFormatter.SignedPct(-Math.Abs(dropFromAnchor))} " +
                    $"so mốc {VipTelegramMessageFormatter.F(anchor)}");
                parts.Add($"P&L so entry {VipTelegramMessageFormatter.SignedPct(currentGain)}");
            }

            return string.Join("\n", parts);
        }

        if (position.EntryBarLow is > 0
            && signal == MasterAlertKinds.SellAll
            && currentGain < 0
            && dropFromAnchor < stop2)
        {
            parts.Add($"Phủ nhận cây vượt đỉnh (thủng {VipTelegramMessageFormatter.F(position.EntryBarLow.Value)})");
            parts.Add($"P&L so entry {VipTelegramMessageFormatter.SignedPct(currentGain)}");
            return string.Join("\n", parts);
        }

        if (MasterAlertExitRegimes.IsUnderBase(regime) && position.OverheadBaseLow is > 0)
        {
            parts.Add(
                $"Mục tiêu cạnh dưới nền {VipTelegramMessageFormatter.F(position.OverheadBaseLow.Value)}" +
                (position.OverheadBaseHigh is > 0
                    ? $"–{VipTelegramMessageFormatter.F(position.OverheadBaseHigh.Value)}"
                    : ""));
            parts.Add(
                $"P&L so entry {VipTelegramMessageFormatter.SignedPct(currentGain)} " +
                $"(peak {VipTelegramMessageFormatter.SignedPct(peakGain)})");
        }
        else if (TopOpportunityVipAlertEvaluator.IsDistributionScan(scan)
                 && dropFromAnchor < (signal == MasterAlertKinds.SellAll ? stop2 : stop1))
        {
            parts.Add("Phân phối: " + GetDistributionLabel(scan));
            parts.Add($"Peak so entry {VipTelegramMessageFormatter.SignedPct(peakGain)}");
        }
        else
        {
            parts.Add(
                $"Rút từ đỉnh {VipTelegramMessageFormatter.SignedPct(-Math.Abs(dropFromAnchor))} " +
                $"so mốc {VipTelegramMessageFormatter.F(anchor)}");
            parts.Add(
                $"P&L so entry {VipTelegramMessageFormatter.SignedPct(currentGain)} " +
                $"(peak {VipTelegramMessageFormatter.SignedPct(peakGain)})");
            var stopPct = signal == MasterAlertKinds.SellPoint1Half ? stop1 : stop2;
            parts.Add($"Phase: {marketPhase} (ngưỡng {stopPct:0.0}%)");
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
        Guid? positionId,
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
                TradeStateReason: opp.TradeStateReason,
                PositionId: positionId),
            cancellationToken);
    }
}
