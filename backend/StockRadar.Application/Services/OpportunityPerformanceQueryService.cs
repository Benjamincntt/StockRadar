using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.MasterAlerts;

namespace StockRadar.Application.Services;

public sealed class OpportunityPerformanceQueryService(
    IWeeklyOpportunityReviewRepository weeklyReviews,
    ISetupTrackRepository tracks,
    IHitCalibrationRepository calibration,
    IFalsePositiveMiningRepository falsePositiveMining,
    IShadowAnalysisRepository shadowAnalysis,
    IEntryTimingRepository entryTiming,
    Microsoft.Extensions.Options.IOptions<ShadowAnalysisOptions> shadowOptions) : IOpportunityPerformanceQueryService
{
    public async Task<AlertHistoryResponseDto> GetAlertHistoryAsync(
        int limit = 50,
        int skip = 0,
        string? status = null,
        string? alertType = null,
        string kind = "buy",
        CancellationToken cancellationToken = default)
    {
        bool? outcomeMeasured = status?.Trim().ToLowerInvariant() switch
        {
            "pending" => false,
            "measured" => true,
            _ => null,
        };

        var buyPointsOnly = !string.Equals(kind, "all", StringComparison.OrdinalIgnoreCase);
        var sourceType = ResolveSourceType(alertType);
        var page = await tracks.GetAlertHistoryAsync(
            limit,
            skip,
            outcomeMeasured,
            sourceType,
            buyPointsOnly,
            cancellationToken);

        var successRate = ComputeOverallSuccessRatePercent(page.TotalSuccess, page.TotalFailed);

        return new AlertHistoryResponseDto(
            successRate,
            page.TotalMeasured,
            page.TotalSuccess,
            page.TotalFailed,
            page.TotalFlat,
            page.TotalPending,
            page.TotalTracked,
            page.Alerts.Select(ToAlertHistoryItem).ToList());
    }

    private static string? ResolveSourceType(string? alertType)
    {
        if (string.IsNullOrWhiteSpace(alertType))
            return null;

        return alertType.Trim() switch
        {
            "Opportunity" or "TopCoHoi" => MasterAlertKinds.Opportunity,
            "BuyPoint1" or "MuaDiem1" => MasterAlertKinds.BuyPoint1,
            "BuyPoint2" or "MuaDiem2" => MasterAlertKinds.BuyPoint2,
            _ => alertType.Trim(),
        };
    }

    private static AlertHistoryItemDto ToAlertHistoryItem(SetupTrackRecord t)
    {
        var status = t.OutcomeMeasured ? MeasurementStatus.Measured : MeasurementStatus.Pending;
        bool? isSuccess = null;
        if (t.OutcomeMeasured)
        {
            isSuccess = t.OutcomeBucket switch
            {
                "Good" => true,
                "Failed" => false,
                _ => null,
            };
        }

        // 15:00 giờ VN — serialize ISO +07:00 (tránh UtcDateTimeConverter gắn Z sai).
        var issuedAt = new DateTimeOffset(
            t.EntryDate.ToDateTime(new TimeOnly(15, 0)),
            TradingCalendar.VietnamOffset);

        return new AlertHistoryItemDto(
            t.Id,
            t.Symbol,
            t.EntryDate,
            t.EntryPrice,
            ToApiAlertType(t.SourceType),
            MasterAlertKinds.Label(t.SourceType),
            issuedAt,
            status,
            t.ForwardPriceT25,
            t.ForwardReturnPercent,
            isSuccess,
            t.OutcomeBucket,
            t.MeasuredAt);
    }

    private static string ToApiAlertType(string sourceType) => sourceType switch
    {
        MasterAlertKinds.Opportunity => "Opportunity",
        MasterAlertKinds.BuyPoint1 => "BuyPoint1",
        MasterAlertKinds.BuyPoint2 => "BuyPoint2",
        _ => sourceType,
    };

    /// <summary>Good / (Good + Failed). Flat & Pending không vào mẫu số — trả 0 khi chưa có quyết định.</summary>
    internal static decimal ComputeOverallSuccessRatePercent(int totalSuccess, int totalFailed)
    {
        var decided = totalSuccess + totalFailed;
        return decided > 0
            ? Math.Round(100m * totalSuccess / decided, 1)
            : 0m;
    }

    public async Task<OpportunityPerformanceSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var calProfile = await calibration.LoadAsync(cancellationToken);
        var calMeta = await calibration.GetMetaAsync(cancellationToken);
        var calibrationDto = calMeta.TotalSamples > 0
            ? new HitCalibrationSummaryDto(
                calMeta.GlobalFactor,
                calMeta.TotalSamples,
                calMeta.PredictionBiasPercent,
                calMeta.UpdatedAt,
                calProfile.Buckets.Select(b => new HitCalibrationBucketDto(
                    b.BucketId,
                    b.SampleCount,
                    b.PredictedMidPercent,
                    b.ActualHitRatePercent,
                    b.CalibrationFactor)).ToList())
            : null;

        var fpResult = await falsePositiveMining.GetLatestAsync(cancellationToken);
        var fpDto = fpResult is null
            ? null
            : new FalsePositiveMiningSummaryDto(
                fpResult.FalsePositiveSetups,
                fpResult.GoodSetups,
                fpResult.Penalties.Select(p => new FalsePositiveCriterionDto(
                    p.ComponentId,
                    p.Label,
                    p.FalsePositiveHits,
                    p.FalsePositiveAvgNorm,
                    p.GoodAvgNorm,
                    p.DeceptionScore,
                    p.WeightPenalty)).ToList());

        var (shadowVariants, shadowMessage) = await BuildShadowAsync(cancellationToken);
        var shadowWeights = await BuildShadowWeightsAsync(cancellationToken);
        var entryTimingDto = await BuildEntryTimingAsync(cancellationToken);

        var review = await weeklyReviews.GetLatestAsync(cancellationToken);
        if (review is null)
        {
            return new OpportunityPerformanceSummaryDto(
                null,
                null,
                null,
                [],
                "Chưa có review tuần. Hệ thống tự chạy thứ Sáu sau phiên hoặc khi đủ dữ liệu T+2.5.",
                calibrationDto,
                fpDto,
                shadowVariants,
                shadowMessage,
                shadowWeights,
                entryTimingDto);
        }

        var outcomes = await tracks.GetForWeekAsync(review.WeekStartDate, cancellationToken);
        return new OpportunityPerformanceSummaryDto(
            review.WeekStartDate,
            review.GeneratedAt,
            ToDto(review),
            outcomes.Take(40).Select(ToDto).ToList(),
            null,
            calibrationDto,
            fpDto,
            shadowVariants,
            shadowMessage,
            shadowWeights,
            entryTimingDto);
    }

    private async Task<EntryTimingSummaryDto?> BuildEntryTimingAsync(CancellationToken cancellationToken)
    {
        var state = await entryTiming.GetAsync(cancellationToken);
        if (state is null || state.TopOnlyMeasured + state.ConfirmMeasured == 0)
            return null;

        var topRate = state.TopOnlyMeasured > 0
            ? Math.Round(100m * state.TopOnlyGood / state.TopOnlyMeasured, 1)
            : 0m;
        var confirmRate = state.ConfirmMeasured > 0
            ? Math.Round(100m * state.ConfirmGood / state.ConfirmMeasured, 1)
            : 0m;

        return new EntryTimingSummaryDto(
            topRate,
            confirmRate,
            state.TopOnlyMeasured,
            state.ConfirmMeasured,
            state.PreferMasterConfirm);
    }

    private async Task<IReadOnlyList<ShadowWeightVariantStatusDto>?> BuildShadowWeightsAsync(
        CancellationToken cancellationToken)
    {
        if (!shadowOptions.Value.Enabled)
            return null;

        var summaries = await shadowAnalysis.GetWeightSummariesAsync(cancellationToken);
        return summaries
            .Select(s => new ShadowWeightVariantStatusDto(
                s.WeightMultiplier,
                s.MeasuredCount,
                s.SuccessRatePercent,
                s.IsProduction,
                s.IsLeader))
            .ToList();
    }

    private async Task<(IReadOnlyList<ShadowVariantStatusDto>? Variants, string? Message)> BuildShadowAsync(
        CancellationToken cancellationToken)
    {
        if (!shadowOptions.Value.Enabled)
            return (null, null);

        var summaries = await shadowAnalysis.GetSummariesAsync(cancellationToken);
        if (summaries.Count == 0)
            return ([], "Shadow mode bật — chờ phân tích + T+2.5");

        var variants = summaries
            .Select(s => new ShadowVariantStatusDto(
                s.VariantMinPassScore,
                s.MeasuredCount,
                s.SuccessRatePercent,
                s.IsProduction,
                s.IsLeader))
            .ToList();

        var leader = summaries.FirstOrDefault(s => s.IsLeader);
        string? message = null;
        if (leader is not null)
        {
            var production = summaries.FirstOrDefault(s => s.IsProduction);
            if (leader.MeasuredCount >= shadowOptions.Value.PromoteAfterMeasuredCount
                && production is not null
                && leader.VariantMinPassScore != production.VariantMinPassScore
                && leader.SuccessRatePercent > production.SuccessRatePercent)
            {
                message =
                    $"Gợi ý thử MinPassScore {leader.VariantMinPassScore} "
                    + $"(win {leader.SuccessRatePercent:0.#}% vs prod {production.SuccessRatePercent:0.#}%)";
            }
            else if (leader.MeasuredCount < shadowOptions.Value.PromoteAfterMeasuredCount)
            {
                message =
                    $"Đang học ({leader.MeasuredCount}/{shadowOptions.Value.PromoteAfterMeasuredCount} setup đo)";
            }
        }

        return (variants, message);
    }

    private static WeeklyOpportunityReviewDto ToDto(WeeklyOpportunityReviewRecord r) => new(
        r.WeekStartDate,
        r.TotalTracked,
        r.MeasuredCount,
        r.GoodCount,
        r.FlatCount,
        r.FailedCount,
        r.SuccessRatePercent,
        r.FailedRatePercent,
        r.OpportunityCount,
        r.BuyPoint1Count,
        r.BuyPoint2Count,
        r.CutLoss1Count,
        r.CutAllCount,
        r.OpportunitySuccessRate,
        r.BuyPoint1SuccessRate,
        r.BuyPoint2SuccessRate,
        r.RecommendedAction,
        r.Summary,
        r.GeneratedAt);

    private static SetupTrackDto ToDto(SetupTrackRecord t) => new(
        t.Id,
        t.Symbol,
        t.SourceType,
        MasterAlertKinds.Label(t.SourceType),
        t.EntryDate,
        t.EntryPrice,
        t.OpportunityRank,
        t.OpportunityScore,
        t.SessionChangePercent,
        t.ForwardReturnPercent,
        t.OutcomeBucket,
        t.MeasuredAt,
        t.PredictedHitPercent,
        t.SetupDna,
        t.ForwardReturnT5,
        t.ForwardReturnT10,
        t.OutcomeBucketT5,
        t.OutcomeBucketT10,
        t.MaxFavorableExcursionPercent,
        t.MaxAdverseExcursionPercent,
        t.HadMasterConfirm);
}
