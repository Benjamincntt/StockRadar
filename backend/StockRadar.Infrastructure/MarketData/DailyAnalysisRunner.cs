using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    IDailyOpportunityRepository opportunities,
    IDailyAnalysisRunRepository analysisRuns,
    IDailyCriterionScoringService criterionScoring,
    ISetupTrackRepository setupTracks,
    IOpportunityPerformanceService performance,
    AdaptiveScoringProfileFactory adaptiveProfileFactory,
    HitCalibrationProfileFactory hitCalibrationProfileFactory,
    ShadowAnalysisService shadowAnalysis,
    IOptions<MarketJobsOptions> options,
    IOptions<PriceRunupFilterOptions> runupFilter,
    IOptions<SmartMoneyOptions> smartMoneyOptions,
    ILogger<DailyAnalysisRunner> logger) : IDailyAnalysisService
{
    public async Task<DailyAnalysisResultDto> RunAsync(
        CancellationToken cancellationToken = default,
        bool runPostProcessing = true)
    {
        var cfg = options.Value.DailyAnalysis;
        var runup = runupFilter.Value;
        var sm = smartMoneyOptions.Value.ToSettings();
        var forTradingDate = TradingCalendar.GetPostSessionAnalysisDate();
        var generatedAt = DateTime.UtcNow;
        var usedRelaxedFallback = false;

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
        logger.LogInformation(
            "VNINDEX {Trend} ({Change:0.##}% / 5d {Change5d:0.##}%), pha {Phase}, loc tang >{MaxGain}% so voi dinh nen.",
            index.Trend,
            index.ChangePercent,
            index.IndexChange5d,
            context.MarketPhase,
            runup.MaxGainFromBasePercent);

        var topSectors = context.SectorSnapshots.Values
            .OrderBy(s => s.Rank)
            .Take(5)
            .Select(s => $"{s.Name}=#{s.Rank}")
            .ToList();
        if (topSectors.Count > 0)
            logger.LogInformation("Ngành mạnh: {Sectors}", string.Join(", ", topSectors));

        var candidates = new List<(Domain.Entities.Stock Stock, SmartMoneyEvaluation Eval)>();
        var runupExcluded = 0;
        foreach (var stock in all)
        {
            var eval = smartMoney.Evaluate(stock, context);
            if (!smartMoney.PassesFilter(eval, sm))
            {
                if (eval.Reasons.Any(r => r.Contains("FOMO", StringComparison.OrdinalIgnoreCase)
                    || r.Contains("so voi", StringComparison.OrdinalIgnoreCase)))
                    runupExcluded++;
                continue;
            }
            if (cfg.MinScore > 0 && eval.Score < cfg.MinScore)
                continue;
            candidates.Add((stock, eval));
        }

        var ordered = candidates
            .Select(c =>
            {
                var decision = buyDecision.Evaluate(c.Stock, context);
                var tradeState = TradeStateResolver.Resolve(
                    decision.Entry,
                    decision.GateFailure,
                    decision.BuyScore,
                    new TradeStateListContext(true));
                var rankInput = OpportunityRankInput.FromEvaluation(
                    decision.BuyScore,
                    decision.PredictedHitPercent,
                    decision.SectorRank,
                    decision.RelativeStrength5d,
                    decision.VolumeRatio,
                    tradeState.State,
                    decision.SetupDna,
                    context.MarketPhase);
                var mlProb = opportunityRanker.PredictWinProbability(rankInput);
                return (c.Stock, c.Eval, decision, tradeState, MlProb: mlProb);
            })
            .OrderByDescending(x => x.MlProb)
            .ThenByDescending(x => x.Eval.Score)
            .ThenBy(x => x.Eval.SectorRank)
            .ThenByDescending(x => x.Eval.RelativeStrength5d)
            .ThenBy(x => x.Stock.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cfg.MaxResults > 0)
            ordered = ordered.Take(cfg.MaxResults).ToList();

        if (opportunityRanker.IsModelActive)
            logger.LogInformation("OpportunityRanker ML active — sort theo P(hit) T+2.5.");
        else
            logger.LogInformation("OpportunityRanker fallback — sort theo heuristic PredictedHitPercent.");

        if (ordered.Count == 0 && cfg.RelaxedFallbackEnabled)
        {
            ordered = BuildRelaxedCandidates(all, context, cfg, sm, opportunityRanker);
            usedRelaxedFallback = ordered.Count > 0;
            if (usedRelaxedFallback)
            {
                logger.LogWarning(
                    "SmartMoney strict = 0 — fallback {Count} mã (Buy Score ≥ {MinScore}).",
                    ordered.Count,
                    cfg.FallbackMinScore);
            }
        }

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
                    ExplainLinesJsonMapper.ToJson(item.decision.TopExplainLines));

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
        await setupTracks.RegisterOpportunitiesAsync(
            forTradingDate,
            built.Select(x => x.seed).ToList(),
            cancellationToken);
        await analysisRuns.UpsertAsync(
            forTradingDate,
            generatedAt,
            all.Count,
            records.Count,
            usedRelaxedFallback,
            cancellationToken);

        logger.LogInformation(
            "Phân tích xong: {Saved} cơ hội cho {ForDate} (từ {Total} mã){Mode}, {RunupExcluded} loại vì vượt nền.",
            records.Count,
            forTradingDate,
            all.Count,
            usedRelaxedFallback ? " [fallback]" : "",
            runupExcluded);

        if (runPostProcessing)
            await RunPostProcessingAsync(forTradingDate, all, index, adaptive, calibration, cancellationToken);

        return new DailyAnalysisResultDto(
            forTradingDate,
            all.Count,
            records.Count,
            generatedAt,
            0,
            usedRelaxedFallback);
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
            logger.LogInformation("Shadow mode: lưu variant MinPassScore cho {ForDate}.", forTradingDate);
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

    private List<(Stock Stock, SmartMoneyEvaluation Eval, BuyDecisionEvaluation decision, TradeStateResult tradeState, decimal MlProb)>
        BuildRelaxedCandidates(
        IReadOnlyList<Stock> all,
        SmartMoneyMarketContext context,
        DailyAnalysisJobOptions cfg,
        SmartMoneySettings sm,
        IOpportunityRanker ranker)
    {
        var maxResults = cfg.FallbackMaxResults > 0 ? cfg.FallbackMaxResults : 15;
        var minScore = cfg.FallbackMinScore > 0 ? cfg.FallbackMinScore : 45;
        var ordered = CollectRelaxedCandidates(all, context, sm, ranker, minScore, maxResults);

        var minResults = cfg.FallbackMinResults;
        if (minResults > 0 && ordered.Count < minResults && minScore > 35)
            ordered = CollectRelaxedCandidates(all, context, sm, ranker, 35, maxResults);

        return ordered;
    }

    private List<(Stock Stock, SmartMoneyEvaluation Eval, BuyDecisionEvaluation decision, TradeStateResult tradeState, decimal MlProb)>
        CollectRelaxedCandidates(
        IReadOnlyList<Stock> all,
        SmartMoneyMarketContext context,
        SmartMoneySettings sm,
        IOpportunityRanker ranker,
        int minBuyScore,
        int maxResults)
    {
        var relaxed = new List<(Stock Stock, SmartMoneyEvaluation Eval, BuyDecisionEvaluation decision, TradeStateResult tradeState, decimal MlProb)>();

        foreach (var stock in all)
        {
            var decision = buyDecision.Evaluate(stock, context);
            if (decision.BuyScore < minBuyScore)
                continue;

            if (stock.History.Count < sm.MinHistoryDays)
                continue;

            if (decision.GateFailure is not null
                && (decision.GateFailure.Contains("phân phối", StringComparison.OrdinalIgnoreCase)
                    || decision.GateFailure.Contains("FOMO", StringComparison.OrdinalIgnoreCase)))
                continue;

            var tradeState = TradeStateResolver.Resolve(
                decision.Entry,
                decision.GateFailure,
                decision.BuyScore,
                new TradeStateListContext(true));
            var rankInput = OpportunityRankInput.FromEvaluation(
                decision.BuyScore,
                decision.PredictedHitPercent,
                decision.SectorRank,
                decision.RelativeStrength5d,
                decision.VolumeRatio,
                tradeState.State,
                decision.SetupDna,
                context.MarketPhase);
            var mlProb = ranker.PredictWinProbability(rankInput);
            relaxed.Add((stock, ToEvaluation(decision), decision, tradeState, mlProb));
        }

        return relaxed
            .OrderByDescending(x => x.MlProb)
            .ThenByDescending(x => x.Eval.Score)
            .ThenBy(x => x.Eval.SectorRank)
            .ThenByDescending(x => x.Eval.RelativeStrength5d)
            .ThenBy(x => x.Stock.Symbol, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .ToList();
    }

    private static SmartMoneyEvaluation ToEvaluation(BuyDecisionEvaluation decision) =>
        new(
            decision.Symbol,
            decision.BuyScore,
            false,
            decision.StockPhase,
            decision.SectorRank,
            decision.RelativeStrength5d,
            decision.VolumeRatio,
            decision.Reasons,
            decision.Signals,
            decision.PredictedHitPercent,
            decision.PredictedSampleCount,
            decision.SetupDna,
            decision.Breakdown);
}
