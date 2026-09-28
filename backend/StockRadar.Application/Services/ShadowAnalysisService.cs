using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Services;

public sealed class ShadowAnalysisService(
    ISmartMoneyOpportunitySelector smartMoney,
    IJobStockRepository stocks,
    IShadowAnalysisRepository shadowRepo,
    IOptions<ShadowAnalysisOptions> shadowOptions,
    IOptions<MarketJobsOptions> jobOptions,
    IOptions<SmartMoneyOptions> smartMoneyOptions,
    IOptions<PriceRunupFilterOptions> runupFilter,
    IOptions<OpportunityPerformanceOptions> performanceOptions)
{
    public async Task RunVariantsAsync(
        DateOnly forTradingDate,
        IReadOnlyList<Stock> universe,
        MarketIndex index,
        AdaptiveScoringProfile adaptive,
        HitCalibrationProfile calibration,
        CancellationToken cancellationToken = default)
    {
        var cfg = shadowOptions.Value;
        if (!cfg.Enabled || cfg.VariantWeightMultipliers.Length == 0)
            return;

        var analysisCfg = jobOptions.Value.DailyAnalysis;
        var runup = runupFilter.Value.ToSettings();
        var productionSm = smartMoneyOptions.Value.ToSettings();
        var multipliers = cfg.VariantWeightMultipliers.Distinct().OrderBy(x => x).ToList();

        foreach (var multiplier in multipliers)
        {
            var scaledAdaptive = multiplier == 1m ? adaptive : adaptive.ScaleMultipliers(multiplier);
            var context = smartMoney.BuildContext(
                universe,
                index,
                runup,
                productionSm,
                scaledAdaptive,
                calibration);

            var candidates = new List<(Stock Stock, SmartMoneyEvaluation Eval)>();
            foreach (var stock in universe)
            {
                var eval = smartMoney.Evaluate(stock, context);
                if (!smartMoney.PassesFilter(eval, productionSm))
                    continue;
                if (analysisCfg.MinScore > 0 && eval.Score < analysisCfg.MinScore)
                    continue;
                candidates.Add((stock, eval));
            }

            var ordered = candidates
                .OrderByDescending(x => x.Eval.PredictedHitPercent)
                .ThenByDescending(x => x.Eval.Score)
                .ThenByDescending(x => (int)x.Eval.SectorWave.Wave)
                .ThenByDescending(x => x.Eval.RelativeStrength5d)
                .ThenBy(x => x.Stock.Symbol, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (analysisCfg.MaxResults > 0)
                ordered = ordered.Take(analysisCfg.MaxResults).ToList();

            var picks = ordered
                .Select((item, rank) => new ShadowPickSeed(
                    item.Stock.Symbol,
                    rank + 1,
                    item.Eval.Score,
                    item.Stock.LatestPrice,
                    item.Eval.PredictedHitPercent))
                .ToList();

            await shadowRepo.ReplaceWeightPicksAsync(
                forTradingDate,
                multiplier,
                picks,
                cancellationToken);
        }
    }

    public async Task<int> MeasurePendingOutcomesAsync(CancellationToken cancellationToken = default)
    {
        var shadowCfg = shadowOptions.Value;
        var perfCfg = performanceOptions.Value;
        if (!shadowCfg.Enabled || !perfCfg.Enabled)
            return 0;

        var today = TradingCalendar.TodayVietnam();
        var measureThrough = TradingSessionMath.AddTradingSessions(today, -perfCfg.MinSessionsBeforeMeasure);

        var stockMap = (await stocks.GetAllAsync(cancellationToken))
            .ToDictionary(s => s.Symbol, StringComparer.OrdinalIgnoreCase);

        return await MeasureWeightOutcomesAsync(measureThrough, stockMap, perfCfg, cancellationToken);
    }

    private async Task<int> MeasureWeightOutcomesAsync(
        DateOnly measureThrough,
        IReadOnlyDictionary<string, Stock> stockMap,
        OpportunityPerformanceOptions perfCfg,
        CancellationToken cancellationToken)
    {
        var pending = await shadowRepo.GetPendingWeightOutcomesAsync(measureThrough, cancellationToken);
        var measured = 0;
        foreach (var pick in pending)
        {
            if (!stockMap.TryGetValue(pick.Symbol, out var stock))
                continue;

            var forward = TradingSessionMath.GetForwardPriceT25(stock.History, pick.ForTradingDate);
            if (forward is null)
                continue;

            var ret = TradingSessionMath.GetForwardReturnPercent(pick.EntryPrice, forward);
            if (ret is null)
                continue;

            await shadowRepo.UpdateWeightOutcomeAsync(
                pick.Id,
                ret.Value,
                ClassifyOutcome(ret.Value, perfCfg),
                cancellationToken);
            measured++;
        }

        if (measured > 0)
        {
            await shadowRepo.RebuildWeightSummariesAsync(
                1m,
                shadowOptions.Value.PromoteAfterMeasuredCount,
                cancellationToken);
        }

        return measured;
    }

    private static string ClassifyOutcome(decimal returnPercent, OpportunityPerformanceOptions cfg)
    {
        if (returnPercent >= cfg.SuccessThresholdPercent)
            return "Good";
        if (returnPercent >= cfg.FlatMinPercent)
            return "Flat";
        return "Failed";
    }
}
