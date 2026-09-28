using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Mapping;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.Services.OpportunityRanking;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>Phân tích SmartMoney sau Job 2 → watchlist phiên mai.</summary>
internal sealed class DailyAnalysisRunner(
    IJobStockRepository stocks,
    IJobMarketIndexProvider marketIndex,
    ISmartMoneyOpportunitySelector smartMoney,
    IBuyDecisionEngine buyDecision,
    IOpportunityRanker opportunityRanker,
    ISignalAnalyzer signals,
    IChartBarProvider chartBars,
    IDailyOpportunityRepository opportunities,
    IEarlyRecoveryRadarRepository earlyRecovery,
    IDailyAnalysisRunRepository analysisRuns,
    IDailyCriterionScoringService criterionScoring,
    ISetupTrackRepository setupTracks,
    IOpportunityPerformanceService performance,
    ISectorWaveRegimeRepository sectorWaveRegimes,
    ISectorWaveRegimeEngine sectorWaveRegimeEngine,
    AdaptiveScoringProfileFactory adaptiveProfileFactory,
    HitCalibrationProfileFactory hitCalibrationProfileFactory,
    ShadowAnalysisService shadowAnalysis,
    IOptions<MarketJobsOptions> options,
    IOptions<PriceRunupFilterOptions> runupFilter,
    IOptions<SmartMoneyOptions> smartMoneyOptions,
    ILogger<DailyAnalysisRunner> logger) : IDailyAnalysisService
{
    /// <summary>Giới hạn số request intraday 15m/1h đồng thời khi quét Top (tránh bóp nghẹt provider).</summary>
    private readonly SemaphoreSlim _intradayGate = new(6, 6);

    public async Task<DailyAnalysisResultDto> RunAsync(
        CancellationToken cancellationToken = default,
        bool runPostProcessing = true,
        bool includeStructureAndTracking = true)
    {
        var cfg = options.Value.DailyAnalysis;
        var runup = runupFilter.Value;
        var sm = smartMoneyOptions.Value.ToSettings();
        var forTradingDate = TradingCalendar.GetPostSessionAnalysisDate();
        var generatedAt = DateTime.UtcNow;

        var index = await marketIndex.GetCurrentAsync(cancellationToken);
        var all = await stocks.GetAllAsync(cancellationToken);
        if (all.Count == 0)
        {
            logger.LogWarning("Phân tích: DB trống — chạy Job 1 trước.");
            return new DailyAnalysisResultDto(forTradingDate, 0, 0, generatedAt, 0);
        }

        logger.LogInformation("Phân tích — {Count} mã universe (DB trực tiếp, không cache API)...", all.Count);

        var adaptive = await adaptiveProfileFactory.LoadAsync(cancellationToken);
        var calibration = await hitCalibrationProfileFactory.LoadAsync(cancellationToken);
        var context = smartMoney.BuildContext(all, index, runup.ToSettings(), sm, adaptive, calibration);
        var detail = context.PhaseDetail;
        logger.LogInformation(
            "VNINDEX {Trend} ({Change:0.##}% / 5d {Change5d:0.##}%), pha {Phase} (AboveMa20={Above}, FTD={Ftd}, HL={Hl}, slopeOk={Slope}), loc tang >{MaxGain}% so voi dinh nen.",
            index.Trend,
            index.ChangePercent,
            index.IndexChange5d,
            context.MarketPhase,
            detail?.CloseAboveMa20,
            detail?.HasFollowThroughDay,
            detail?.HasHigherLow,
            detail?.Ma20SlopeNonNegative,
            runup.MaxGainFromBasePercent);

        var waveSectors = context.SectorSnapshots.Values
            .Where(s => s.HasWave)
            .OrderByDescending(s => (int)s.Wave)
            .ThenByDescending(s => s.WaveScore)
            .Take(5)
            .Select(s => $"{s.Name}={s.WaveLabel} ({s.BreadthDetail})")
            .ToList();
        logger.LogInformation("Sóng ngành: {Sectors}",
            waveSectors.Count > 0 ? string.Join(", ", waveSectors) : "không ngành nào có sóng");

        var activeSectorRegimes = await AdvanceSectorWaveRegimesAsync(
            context.SectorSnapshots, forTradingDate, sm.SectorWaveThresholds, cancellationToken);
        context = context with { ActiveSectorRegimes = activeSectorRegimes };
        if (activeSectorRegimes.Count > 0)
            logger.LogInformation(
                "Sóng ngành (regime, kế thừa nhiều phiên): {Sectors}",
                string.Join(", ", activeSectorRegimes));

        var candidates = new List<(Domain.Entities.Stock Stock, SmartMoneyEvaluation Eval, BuyDecisionEvaluation Decision)>();
        var runupExcluded = 0;
        var gateStats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        void CountGate(string gate) =>
            gateStats[gate] = gateStats.GetValueOrDefault(gate) + 1;
        foreach (var stock in all)
        {
            // Pass 1: cổng rẻ (bỏ phân kỳ, không I/O). Chỉ khi qua hết mới cần kiểm tra phân kỳ.
            var preDecision = buyDecision.Evaluate(stock, context, requireDivergence: false);
            BuyDecisionEvaluation decision;
            if (preDecision.PassesTopFilter)
            {
                // Pass 2: đạt cổng rẻ → gọi intraday 15m/1h (throttle + timeout + fallback 1D).
                var (m15, m1h) = await FetchIntradayAsync(stock.Symbol, cancellationToken);
                decision = buyDecision.Evaluate(stock, context, m15, m1h, requireDivergence: true);
            }
            else
            {
                decision = preDecision;
            }

            var eval = smartMoney.Evaluate(stock, context, decision);
            if (!smartMoney.PassesFilter(eval, sm))
            {
                CountGate(GateFailureClassifier.Classify(eval.Reasons.FirstOrDefault()));
                if (eval.Reasons.Any(r => r.Contains("FOMO", StringComparison.OrdinalIgnoreCase)))
                    runupExcluded++;
                continue;
            }
            if (cfg.MinScore > 0 && eval.Score < cfg.MinScore)
            {
                CountGate(GateFailureClassifier.BelowJobMinScoreGate);
                continue;
            }
            candidates.Add((stock, eval, decision));
        }

        var ordered = candidates
            .Select(c =>
            {
                var decision = c.Decision;
                var tradeState = TradeStateResolver.Resolve(
                    decision.Entry,
                    decision.GateFailure,
                    decision.BuyScore,
                    new TradeStateListContext(true));
                var (atrPct, distMa20) = ComputeAtrAndDistMa20(c.Stock.History);
                var rankInput = OpportunityRankInput.FromEvaluation(
                    decision.BuyScore,
                    decision.PredictedHitPercent,
                    decision.SectorWave.WaveRank,
                    decision.RelativeStrength5d,
                    decision.VolumeRatio,
                    tradeState.State,
                    decision.SetupDna,
                    context.MarketPhase,
                    atrPct,
                    distMa20);
                var mlProb = opportunityRanker.PredictWinProbability(rankInput);
                return (c.Stock, c.Eval, decision, tradeState, MlProb: mlProb);
            })
            .OrderByDescending(x => x.MlProb)
            .ThenByDescending(x => x.Eval.Score)
            .ThenByDescending(x => (int)x.Eval.SectorWave.Wave)
            .ThenByDescending(x => x.Eval.RelativeStrength5d)
            .ThenBy(x => x.Stock.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ordered = ApplyTopHygiene(ordered, context.MarketPhase, cfg, out var hygieneStats);
        if (hygieneStats.Rejected > 0)
        {
            logger.LogInformation(
                "Top hygiene ({Phase}): loại {Rejected} (await={Await}, regime={Regime}); giữ {Kept}.",
                context.MarketPhase,
                hygieneStats.Rejected,
                hygieneStats.RejectedAwaiting,
                hygieneStats.RejectedRegime,
                hygieneStats.Kept);
        }

        if (cfg.MaxResults > 0)
            ordered = ordered.Take(cfg.MaxResults).ToList();

        if (opportunityRanker.IsModelActive)
            logger.LogInformation("OpportunityRanker ML active — sort theo P(hit) T+2.5.");
        else
            logger.LogInformation("OpportunityRanker fallback — sort theo heuristic PredictedHitPercent.");

        var built = ordered
            .Select((item, rank) =>
            {
                var legacyRecommendation = TradeStateLabels
                    .ToLegacyRecommendation(item.tradeState.State, item.decision.BuyScore)
                    .ToString();

                var record = new DailyOpportunityRecord(
                    forTradingDate,
                    rank + 1,
                    item.Stock.Symbol,
                    item.Stock.Name,
                    item.Stock.Sector,
                    item.Eval.Score,
                    item.Stock.LatestPrice,
                    signals.GetChangePercent(item.Stock, 1),
                    item.Eval.VolumeRatio,
                    generatedAt,
                    item.decision.BuyScore,
                    item.MlProb,
                    item.decision.PredictedSampleCount,
                    item.decision.SetupDna,
                    legacyRecommendation,
                    item.tradeState.State.ToString(),
                    item.tradeState.Reason,
                    EntryPointJsonMapper.ToJson(DtoMapper.ToDto(item.decision.Entry)),
                    ExplainLinesJsonMapper.ToJson(item.decision.TopExplainLines),
                    AverageDailyVolume: (long)signals.GetAverageVolume(item.Stock.History, 20),
                    MarketPhase: context.MarketPhase.ToString());

                var seed = new OpportunityTrackSeed(
                    item.Stock.Symbol,
                    rank + 1,
                    item.Eval.Score,
                    item.Stock.LatestPrice,
                    signals.GetChangePercent(item.Stock, 1),
                    item.MlProb,
                    item.decision.SetupDna,
                    BuyScoreBreakdownMapper.ToJson(item.Eval.Breakdown),
                    item.tradeState.State.ToString(),
                    item.tradeState.Reason);

                return (record, seed);
            })
            .ToList();

        var records = built.Select(x => x.record).ToList();

        await opportunities.ReplaceForDateAsync(forTradingDate, records, cancellationToken);

        var topSymbols = new HashSet<string>(
            records.Select(r => r.Symbol),
            StringComparer.OrdinalIgnoreCase);
        var radarRecords = BuildEarlyRecoveryRadar(
            all,
            context,
            sm,
            forTradingDate,
            generatedAt,
            topSymbols);
        await earlyRecovery.ReplaceForDateAsync(forTradingDate, radarRecords, cancellationToken);

        if (includeStructureAndTracking)
        {
            await setupTracks.RegisterOpportunitiesAsync(
                forTradingDate,
                built.Select(x => x.seed).ToList(),
                cancellationToken);
        }

        await analysisRuns.UpsertAsync(
            forTradingDate,
            generatedAt,
            all.Count,
            records.Count,
            GateStatsJsonMapper.ToJson(gateStats),
            cancellationToken);

        logger.LogInformation(
            "Phân tích xong: {Saved} cơ hội cho {ForDate} (từ {Total} mã), {RunupExcluded} loại vì vượt nền, {Radar} Early Recovery Radar.",
            records.Count,
            forTradingDate,
            all.Count,
            runupExcluded,
            radarRecords.Count);

        if (gateStats.Count > 0)
            logger.LogInformation(
                "Gate stats ({Total} mã bị loại): {GateStats}",
                gateStats.Values.Sum(),
                string.Join(", ", gateStats.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}")));

        if (!includeStructureAndTracking)
            logger.LogInformation("Phân tích light — bỏ SetupTracks (intraday refresh).");

        if (runPostProcessing)
            await RunPostProcessingAsync(forTradingDate, all, index, adaptive, calibration, cancellationToken);

        return new DailyAnalysisResultDto(
            forTradingDate,
            all.Count,
            records.Count,
            generatedAt);
    }

    /// <summary>
    /// Mã Loose MA (Close&gt;MA20, MA20 slope≥0) nhưng chưa đủ RS để mua → theo dõi ngầm.
    /// </summary>
    private List<EarlyRecoveryRecord> BuildEarlyRecoveryRadar(
        IReadOnlyList<Stock> all,
        SmartMoneyMarketContext context,
        SmartMoneySettings sm,
        DateOnly forTradingDate,
        DateTime generatedAt,
        HashSet<string> topSymbols)
    {
        var radar = new List<EarlyRecoveryRecord>();
        var phase = context.MarketPhase.ToString();

        foreach (var stock in all)
        {
            if (topSymbols.Contains(stock.Symbol))
                continue;

            var history = stock.History;
            if (history.Count < sm.MinHistoryDays)
                continue;

            var hasLooseMa = signals.HasBullishMaStack(
                history,
                MaStackStrictness.Loose,
                sm.MinSessionsForMa50,
                sm.MinSessionsForFullStack);
            if (!hasLooseMa)
                continue;

            var rs5 = signals.GetRelativeStrength(stock, context.IndexChangePercent5d, 5);
            var pct = context.RsPercentile.GetValueOrDefault(stock.Symbol, 0m);
            if (pct >= sm.MinRsPercentileForUnfavorable && rs5 > 0m)
                continue;

            var reason = pct < sm.MinRsPercentileForUnfavorable && rs5 <= 0m
                ? $"RS percentile {pct:0.#} < {sm.MinRsPercentileForUnfavorable} và RS5 {rs5:0.##} ≤ 0"
                : pct < sm.MinRsPercentileForUnfavorable
                    ? $"RS percentile {pct:0.#} < {sm.MinRsPercentileForUnfavorable}"
                    : $"RS5 {rs5:0.##} ≤ 0 (yếu hơn / ngang VNINDEX)";

            radar.Add(new EarlyRecoveryRecord(
                forTradingDate,
                stock.Symbol,
                stock.Name,
                stock.Sector,
                stock.LatestPrice,
                signals.GetChangePercent(stock, 1),
                signals.GetVolumeRatio(history),
                rs5,
                pct,
                phase,
                reason,
                generatedAt));
        }

        return radar
            .OrderByDescending(r => r.RsPercentile)
            .ThenByDescending(r => r.Rs5)
            .ThenBy(r => r.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task RunPostProcessingAsync(
        DateOnly forTradingDate,
        IReadOnlyList<Stock> all,
        MarketIndex index,
        AdaptiveScoringProfile adaptive,
        HitCalibrationProfile calibration,
        CancellationToken cancellationToken)
    {
        try
        {
            await shadowAnalysis.RunVariantsAsync(
                forTradingDate,
                all,
                index,
                adaptive,
                calibration,
                cancellationToken);
            logger.LogInformation("Shadow mode: lưu variant trọng số cho {ForDate}.", forTradingDate);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Shadow mode thất bại — bỏ qua.");
        }

        try
        {
            var scored = await criterionScoring.RunAfterAnalysisAsync(cancellationToken);
            logger.LogInformation("Chấm điểm tiêu chí T-1: {Count} mã lưu snapshot.", scored);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chấm điểm tiêu chí thất bại — bỏ qua.");
        }

        try
        {
            await performance.MeasurePendingOutcomesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Đo hiệu quả T+2.5 thất bại — bỏ qua.");
        }
    }

    /// <summary>
    /// Nạp nội nến 15m + 1h cho một mã để xét cổng phân kỳ. Best-effort: timeout/loc sẽ
    /// trả mảng rỗng → engine chỉ dùng khung ngày (fallback an toàn, không chặn cả job).
    /// </summary>
    private async Task<(List<OhlcvBar> M15, List<OhlcvBar> M1H)> FetchIntradayAsync(
        string symbol,
        CancellationToken cancellationToken)
    {
        var m15 = await FetchBarsSafeAsync(symbol, "15m", cancellationToken);
        var m1h = await FetchBarsSafeAsync(symbol, "1h", cancellationToken);
        return (m15, m1h);
    }

    private async Task<List<OhlcvBar>> FetchBarsSafeAsync(
        string symbol,
        string interval,
        CancellationToken cancellationToken)
    {
        await _intradayGate.WaitAsync(cancellationToken);
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(8));
            var bars = await chartBars.FetchAsync(symbol, interval, cts.Token);
            var converted = new List<OhlcvBar>(bars.Count);
            foreach (var b in bars)
            {
                var bar = ToOhlcvBar(b);
                if (bar is not null)
                    converted.Add(bar);
            }
            return converted;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Intraday {Interval} {Symbol} lỗi — fallback phân kỳ khung ngày.", interval, symbol);
            return new List<OhlcvBar>();
        }
        finally
        {
            _intradayGate.Release();
        }
    }

    private static OhlcvBar? ToOhlcvBar(ChartBarDto b)
    {
        // ChartBarDto.Time ở định dạng ISO round-trip ("o"), vd "2026-09-28T09:15:00".
        if (string.IsNullOrWhiteSpace(b.Time)
            || !DateTime.TryParse(b.Time, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return null;
        return new OhlcvBar(DateOnly.FromDateTime(dt), b.Open, b.High, b.Low, b.Close, b.Volume);
    }

    private sealed record TopHygieneStats(int Kept, int Rejected, int RejectedAwaiting, int RejectedRegime);

    private static List<(Stock Stock, SmartMoneyEvaluation Eval, BuyDecisionEvaluation decision, TradeStateResult tradeState, decimal MlProb)>
        ApplyTopHygiene(
            List<(Stock Stock, SmartMoneyEvaluation Eval, BuyDecisionEvaluation decision, TradeStateResult tradeState, decimal MlProb)> ordered,
            MarketWyckoffPhase phase,
            DailyAnalysisJobOptions cfg,
            out TopHygieneStats stats)
    {
        var kept = new List<(Stock Stock, SmartMoneyEvaluation Eval, BuyDecisionEvaluation decision, TradeStateResult tradeState, decimal MlProb)>();
        var rejectedAwaiting = 0;
        var rejectedRegime = 0;

        foreach (var item in ordered)
        {
            if (!PassesTopHygiene(item.decision, item.tradeState, phase, cfg, out var reason))
            {
                if (reason == "awaiting")
                    rejectedAwaiting++;
                else if (reason == "regime")
                    rejectedRegime++;
                continue;
            }

            kept.Add(item);
        }

        var rejected = rejectedAwaiting + rejectedRegime;
        stats = new TopHygieneStats(kept.Count, rejected, rejectedAwaiting, rejectedRegime);
        return kept;
    }

    private static bool PassesTopHygiene(
        BuyDecisionEvaluation decision,
        TradeStateResult tradeState,
        MarketWyckoffPhase phase,
        DailyAnalysisJobOptions cfg,
        out string? rejectReason)
    {
        rejectReason = null;

        // Leader RS (breakout + RS vượt trội, dẫn dắt trước VNINDEX) được miễn mọi chặn Top
        // theo pha: giữ cả khi còn AwaitingTrigger hay thị trường Unfavorable.
        if (decision.IsRsLeader)
            return true;

        if (cfg.ExcludeAwaitingTriggerFromTop && tradeState.State == StockTradeState.AwaitingTrigger)
        {
            rejectReason = "awaiting";
            return false;
        }

        if (IsBreakoutSetup(decision) && !PassesRegimeBreakoutGate(decision, tradeState, phase, cfg))
        {
            rejectReason = "regime";
            return false;
        }

        return true;
    }

    private static bool IsBreakoutSetup(BuyDecisionEvaluation decision)
    {
        if (decision.Entry.Type == EntryPointType.Breakout)
            return true;

        var dna = decision.SetupDna ?? "";
        return dna.StartsWith("Breakout", StringComparison.OrdinalIgnoreCase)
            || dna.Contains("Phá vỡ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool PassesRegimeBreakoutGate(
        BuyDecisionEvaluation decision,
        TradeStateResult tradeState,
        MarketWyckoffPhase phase,
        DailyAnalysisJobOptions cfg)
    {
        return phase switch
        {
            // Favorable: breakout được phép
            MarketWyckoffPhase.Favorable => true,
            // Neutral/Unfavorable: chỉ breakout đã Actionable (không còn sàn điểm riêng)
            MarketWyckoffPhase.Neutral => tradeState.State == StockTradeState.Actionable,
            MarketWyckoffPhase.Unfavorable => tradeState.State == StockTradeState.Actionable,
            _ => tradeState.State == StockTradeState.Actionable,
        };
    }

    /// <summary>
    /// Tính + lưu trạng thái Sóng ngành xuyên phiên (spec 007) cho từng ngành có snapshot hôm nay.
    /// Ngành thiếu dữ liệu hôm nay (dưới MinStocksPerSector) không được advance — giữ nguyên trạng
    /// thái bản ghi gần nhất, trung lập theo đúng edge case đã spec.
    /// </summary>
    private async Task<HashSet<string>> AdvanceSectorWaveRegimesAsync(
        IReadOnlyDictionary<string, SectorSnapshot> sectorSnapshots,
        DateOnly tradingDate,
        SectorWaveSettings settings,
        CancellationToken cancellationToken)
    {
        var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (sector, snapshot) in sectorSnapshots)
        {
            var previous = await sectorWaveRegimes.GetLatestAsync(sector, cancellationToken);
            if (previous is not null && previous.TradingDate == tradingDate)
            {
                if (previous.IsActive)
                    active.Add(sector);
                continue;
            }

            var next = sectorWaveRegimeEngine.Advance(sector, previous, snapshot, tradingDate, settings);
            await sectorWaveRegimes.UpsertAsync(next, cancellationToken);
            if (next.IsActive)
                active.Add(sector);
        }

        return active;
    }

    /// <summary>Tính ATR14% và khoảng cách MA20 từ lịch sử OHLCV tại phiên cuối.</summary>
    private static (decimal AtrPct, decimal DistMa20) ComputeAtrAndDistMa20(
        IReadOnlyList<OhlcvBar> history)
    {
        if (history.Count < 5)
            return (0m, 0m);

        var idx = history.Count - 1;
        var bar = history[idx];

        // ATR14 — dùng chung công thức với toàn hệ thống, không tự cài lại.
        var atr = IndicatorMath.Atr(history, 14);
        var atrPct = bar.Close > 0 ? atr / bar.Close * 100m : 0m;

        // Dist MA20 — dùng chung công thức SMA, không tự cài lại.
        var distMa20 = 0m;
        if (Math.Min(20, idx + 1) >= 5)
        {
            var ma20 = IndicatorMath.SmaAt(history, idx, 20);
            if (ma20 > 0)
                distMa20 = (bar.Close - ma20) / ma20 * 100m;
        }

        return (atrPct, distMa20);
    }
}