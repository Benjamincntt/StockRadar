using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Domain.Services;

namespace StockRadar.Infrastructure.MarketData;

internal sealed class OpportunityPerformanceRunner(
    ISetupTrackRepository tracks,
    IWeeklyOpportunityReviewRepository weeklyReviews,
    IJobStockRepository stocks,
    HitCalibrationService hitCalibration,
    FalsePositiveMiningService falsePositiveMining,
    ShadowAnalysisService shadowAnalysis,
    EntryTimingService entryTiming,
    IOptions<OpportunityPerformanceOptions> options,
    IOptions<SwingTradingOptions> swingOptions,
    ILogger<OpportunityPerformanceRunner> logger) : IOpportunityPerformanceService
{
    public async Task<int> MeasurePendingOutcomesAsync(CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        if (!cfg.Enabled)
            return 0;

        var today = TradingCalendar.TodayVietnam();
        var measureThrough = TradingSessionMath.AddTradingSessions(today, -cfg.MinSessionsBeforeMeasure);
        var pending = await tracks.GetPendingOutcomesAsync(measureThrough, cancellationToken);

        var stockMap = (await stocks.GetAllAsync(cancellationToken))
            .ToDictionary(s => s.Symbol, StringComparer.OrdinalIgnoreCase);

        var measured = 0;
        if (pending.Count > 0)
        {
        foreach (var track in pending)
        {
            if (!stockMap.TryGetValue(track.Symbol, out var stock))
                continue;

            var forward = TradingSessionMath.GetForwardPriceT25(stock.History, track.EntryDate);
            if (forward is null)
                continue;

            var ret = TradingSessionMath.GetForwardReturnPercent(track.EntryPrice, forward);
            if (ret is null)
                continue;

            var bucket = ClassifyOutcome(ret.Value, cfg);
            var weekStart = CriterionReviewHelper.GetWeekStart(track.EntryDate);
            var hadConfirm = track.SourceType == MasterAlertKinds.Opportunity
                ? await tracks.HasMasterConfirmAsync(track.Symbol, track.EntryDate, cancellationToken)
                : (bool?)null;
            await tracks.UpdateOutcomeAsync(
                track.Id,
                forward.Value,
                ret.Value,
                bucket,
                weekStart,
                hadConfirm,
                cancellationToken);
            measured++;
        }
        }

        if (measured > 0)
            logger.LogInformation("Đo hiệu quả T+2.5: {Count}/{Total} setup.", measured, pending.Count);

        var swingMeasured = await MeasureSwingMetricsAsync(cancellationToken);
        if (swingMeasured > 0)
            logger.LogInformation("Đo swing T+5/T+10 + MFE/MAE: {Count} setup.", swingMeasured);

        if (measured > 0 || swingMeasured > 0)
        {
            try
            {
                var profile = await hitCalibration.RebuildAsync(cancellationToken);
                logger.LogInformation(
                    "Calibration P(hit): {Samples} mẫu, factor {Factor:0.###}.",
                    profile.TotalSamples,
                    profile.GlobalFactor);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Calibration P(hit) thất bại — bỏ qua.");
            }

            try
            {
                var fp = await falsePositiveMining.RunAndApplyAsync(cancellationToken);
                if (fp.HasActionablePenalties)
                {
                    logger.LogInformation(
                        "False positive mining: {Fp} xịt / {Good} tốt — giảm weight {Count} tiêu chí.",
                        fp.FalsePositiveSetups,
                        fp.GoodSetups,
                        fp.Penalties.Count);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "False positive mining thất bại — bỏ qua.");
            }

            try
            {
                await entryTiming.RebuildAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Entry timing rebuild thất bại — bỏ qua.");
            }

            try
            {
                var shadowMeasured = await shadowAnalysis.MeasurePendingOutcomesAsync(cancellationToken);
                if (shadowMeasured > 0)
                    logger.LogInformation("Shadow mode đo T+2.5: {Count} pick.", shadowMeasured);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Shadow mode đo T+2.5 thất bại — bỏ qua.");
            }
        }

        return measured;
    }

    private async Task<int> MeasureSwingMetricsAsync(CancellationToken cancellationToken)
    {
        var swingCfg = swingOptions.Value;
        var perfCfg = options.Value;
        if (!perfCfg.Enabled)
            return 0;

        var today = TradingCalendar.TodayVietnam();
        var measureThrough = TradingSessionMath.AddTradingSessions(
            today,
            -swingCfg.SwingMeasureSessionsLong);
        var pending = await tracks.GetPendingSwingMetricsAsync(measureThrough, cancellationToken);
        if (pending.Count == 0)
            return 0;

        var stockMap = (await stocks.GetAllAsync(cancellationToken))
            .ToDictionary(s => s.Symbol, StringComparer.OrdinalIgnoreCase);

        var measured = 0;
        foreach (var track in pending)
        {
            if (!stockMap.TryGetValue(track.Symbol, out var stock))
                continue;

            var path = TradingSessionMath.ComputeSwingPath(
                stock.History,
                track.EntryDate,
                track.EntryPrice,
                swingCfg.SwingMeasureSessionsShort,
                swingCfg.SwingMeasureSessionsLong);

            if (path.ReturnT5 is null && path.ReturnT10 is null)
                continue;

            await tracks.UpdateSwingMetricsAsync(
                track.Id,
                path.ReturnT5,
                path.ReturnT10,
                path.ReturnT5 is null ? null : ClassifyOutcome(path.ReturnT5.Value, perfCfg),
                path.ReturnT10 is null ? null : ClassifyOutcome(path.ReturnT10.Value, perfCfg),
                path.MaxFavorableExcursionPercent,
                path.MaxAdverseExcursionPercent,
                cancellationToken);
            measured++;
        }

        return measured;
    }

    public async Task<WeeklyOpportunityReviewRecord?> RunWeeklyReviewAsync(
        DateOnly? weekStart = null,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        if (!cfg.Enabled)
            return null;

        await MeasurePendingOutcomesAsync(CancellationToken.None);

        var targetWeek = weekStart ?? CriterionReviewHelper.GetWeekStart(
            TradingCalendar.TodayVietnam().AddDays(-7));
        var rows = await tracks.GetForWeekAsync(targetWeek, cancellationToken);
        if (rows.Count == 0)
        {
            logger.LogInformation("Review tuần {Week}: chưa có kết quả đo.", targetWeek);
            return null;
        }

        var good = rows.Count(r => r.OutcomeBucket == OutcomeBuckets.Good);
        var flat = rows.Count(r => r.OutcomeBucket == OutcomeBuckets.Flat);
        var failed = rows.Count(r => r.OutcomeBucket == OutcomeBuckets.Failed);
        var measured = good + flat + failed;
        var successRate = measured > 0 ? Math.Round((decimal)good / measured * 100m, 1) : 0m;
        var failedRate = measured > 0 ? Math.Round((decimal)failed / measured * 100m, 1) : 0m;

        var oppRows = rows.Where(r => r.SourceType == MasterAlertKinds.Opportunity).ToList();
        var buy1Rows = rows.Where(r => r.SourceType == MasterAlertKinds.BuyPoint1).ToList();
        var buy2Rows = rows.Where(r => r.SourceType == MasterAlertKinds.BuyPoint2).ToList();

        var review = new WeeklyOpportunityReviewRecord(
            targetWeek,
            rows.Count,
            measured,
            good,
            flat,
            failed,
            successRate,
            failedRate,
            oppRows.Count,
            buy1Rows.Count,
            buy2Rows.Count,
            rows.Count(r => r.SourceType == MasterAlertKinds.CutLoss1),
            rows.Count(r => r.SourceType == MasterAlertKinds.CutAll),
            SuccessRateFor(oppRows),
            SuccessRateFor(buy1Rows),
            SuccessRateFor(buy2Rows),
            RecommendAction(failedRate, cfg.MaxFailedRatePercent),
            BuildSummary(good, flat, failed, successRate, failedRate, oppRows.Count, buy1Rows.Count),
            DateTime.UtcNow);

        await weeklyReviews.UpsertAsync(review, cancellationToken);
        logger.LogInformation(
            "Review tuần {Week}: {Good} tốt / {Flat} đi ngang / {Failed} xịt ({Success}% thành công).",
            targetWeek,
            good,
            flat,
            failed,
            successRate);

        return review;
    }

    private static string ClassifyOutcome(decimal returnPercent, OpportunityPerformanceOptions cfg)
    {
        if (returnPercent >= cfg.SuccessThresholdPercent)
            return OutcomeBuckets.Good;

        if (returnPercent >= cfg.FlatMinPercent)
            return OutcomeBuckets.Flat;

        return OutcomeBuckets.Failed;
    }

    private static decimal SuccessRateFor(IReadOnlyList<SetupTrackRecord> rows)
    {
        if (rows.Count == 0)
            return 0;

        var good = rows.Count(r => r.OutcomeBucket == OutcomeBuckets.Good);
        return Math.Round((decimal)good / rows.Count * 100m, 1);
    }

    private static string RecommendAction(decimal failedRate, decimal maxFailedRate) =>
        failedRate >= maxFailedRate ? "Overhaul" : failedRate >= maxFailedRate * 0.75m ? "Review" : "Keep";

    private static string BuildSummary(
        int good,
        int flat,
        int failed,
        decimal successRate,
        decimal failedRate,
        int oppCount,
        int buy1Count) =>
        $"T+2.5: {good} tăng tốt, {flat} đi ngang, {failed} xịt · {successRate}% thành công · " +
        $"{failedRate}% hỏng · Top {oppCount} mã · Mua điểm 1: {buy1Count} lần.";
}

internal static class OutcomeBuckets
{
    public const string Good = "Good";
    public const string Flat = "Flat";
    public const string Failed = "Failed";
}
