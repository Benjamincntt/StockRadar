using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

public sealed class OpportunityPerformanceQueryService(
    IWeeklyOpportunityReviewRepository weeklyReviews,
    ISetupTrackRepository tracks,
    IHitCalibrationRepository calibration,
    IFalsePositiveMiningRepository falsePositiveMining,
    IShadowAnalysisRepository shadowAnalysis,
    IEntryTimingRepository entryTiming,
    IOptions<ShadowAnalysisOptions> shadowOptions,
    IMasterAlertPositionRepository masterPositions,
    IOptions<RealizedPnlOptions> realizedOptions) : IOpportunityPerformanceQueryService
{
    private const string MethodologyNote =
        "Lợi nhuận thực = giá tại tín hiệu Bán nửa/Bán hết, trừ phí mua + phí/thuế bán. " +
        "Trọng số theo size thực bán. Chỉ tính lệnh đã đóng; lệnh còn mở vẫn đo bằng T+2.5.";

    public async Task<AlertHistoryResponseDto> GetAlertHistoryAsync(
        int limit = 50,
        int skip = 0,
        string? status = null,
        string? alertType = null,
        string kind = "buy",
        DateOnly? from = null,
        DateOnly? to = null,
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
            from,
            to,
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
            page.Alerts.Select(ToAlertHistoryItem).ToList(),
            page.TotalClosedTrades,
            page.TotalOpenTrades,
            page.RealizedWinCount,
            page.RealizedLoseCount,
            page.RealizedFlatCount,
            page.RealizedWinRatePercent,
            page.AvgRealizedReturnPercent);
    }

    public async Task<AlertHistoryTrendsResponseDto> GetAlertHistoryTrendsAsync(
        string period = "week",
        string kind = "buy",
        int limit = 12,
        DateOnly? selectedPeriodStart = null,
        CancellationToken cancellationToken = default)
    {
        var buyPointsOnly = !string.Equals(kind, "all", StringComparison.OrdinalIgnoreCase);
        var rows = await tracks.GetAlertHistoryTracksAsync(buyPointsOnly, null, cancellationToken);
        return AlertHistoryTrendBuilder.Build(period, rows, limit, selectedPeriodStart);
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

        var positionStatus = t.PositionId is null ? "None" : t.PositionIsClosed == true ? "Closed" : "Open";
        bool? realizedIsSuccess = t.RealizedOutcomeBucket switch
        {
            OutcomeBucketNames.Good => true,
            OutcomeBucketNames.Failed => false,
            _ => null,
        };

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
            t.MeasuredAt,
            t.PositionId,
            positionStatus,
            t.RealizedReturnPercent,
            t.RealizedWeightedReturnPercent,
            t.RealizedOutcomeBucket,
            realizedIsSuccess,
            t.Sell1Price,
            t.Sell1Date,
            t.SellAllPrice,
            t.SellAllDate,
            t.HoldingSessions,
            t.RealizedStatus);
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
        var realizedDto = await BuildRealizedAsync(cancellationToken);

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
                entryTimingDto,
                realizedDto);
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
            entryTimingDto,
            realizedDto);
    }

    /// <summary>
    /// Tổng hợp realized P&amp;L cho card "Lợi nhuận thực" — tính từ MasterAlertPositions (1 dòng = 1 lệnh).
    /// Cùng phạm vi lookback với <see cref="RealizedPnlService.MeasureClosedPositionsAsync"/>
    /// (<c>RealizedPnlOptions.MeasureLookbackSessions</c>).
    /// </summary>
    private async Task<RealizedPnlSummaryDto> BuildRealizedAsync(CancellationToken cancellationToken)
    {
        var cfg = realizedOptions.Value;
        var today = TradingCalendar.TodayVietnam();
        var fromDate = TradingSessionMath.SubtractTradingSessions(today, cfg.MeasureLookbackSessions);

        var allPositions = await masterPositions.GetPositionsSinceAsync(fromDate, cancellationToken);
        var openTrades = allPositions.Count(p => !p.IsClosed);
        var closedTrades = allPositions.Count(p => p.IsClosed);

        var measuredClosed = allPositions
            .Where(p => p.IsClosed && p.RealizedMeasured)
            .ToList();
        var missingSellPriceCount = measuredClosed.Count(p => p.RealizedStatus == RealizedStatusNames.MissingSellPrice);
        var approximateCount = measuredClosed.Count(p => p.RealizedStatus == RealizedStatusNames.Approximate);

        var eligible = cfg.IncludeApproximateInAggregates
            ? measuredClosed.Where(p => p.RealizedStatus != RealizedStatusNames.MissingSellPrice).ToList()
            : measuredClosed.Where(p => p.RealizedStatus == RealizedStatusNames.Measured).ToList();

        var winCount = eligible.Count(p => p.RealizedOutcomeBucket == OutcomeBucketNames.Good);
        var loseCount = eligible.Count(p => p.RealizedOutcomeBucket == OutcomeBucketNames.Failed);
        var flatCount = eligible.Count(p => p.RealizedOutcomeBucket == OutcomeBucketNames.Flat);
        var winRate = ComputeOverallSuccessRatePercent(winCount, loseCount);

        var returns = eligible
            .Where(p => p.RealizedReturnOnDeployedPercent is not null)
            .Select(p => p.RealizedReturnOnDeployedPercent!.Value)
            .OrderBy(v => v)
            .ToList();
        decimal? avgReturn = returns.Count > 0 ? Math.Round(returns.Average(), 2) : null;
        decimal? medianReturn = returns.Count > 0 ? Median(returns) : null;

        var totalWeighted = eligible
            .Where(p => p.RealizedWeightedReturnPercent is not null)
            .Sum(p => p.RealizedWeightedReturnPercent!.Value);

        var holdingSessions = eligible
            .Where(p => p.HoldingSessions is not null)
            .Select(p => p.HoldingSessions!.Value)
            .ToList();
        decimal? avgHolding = holdingSessions.Count > 0 ? Math.Round((decimal)holdingSessions.Average(), 1) : null;

        var best = eligible
            .Where(p => p.RealizedReturnOnDeployedPercent is not null)
            .OrderByDescending(p => p.RealizedReturnOnDeployedPercent!.Value)
            .FirstOrDefault();
        var worst = eligible
            .Where(p => p.RealizedReturnOnDeployedPercent is not null)
            .OrderBy(p => p.RealizedReturnOnDeployedPercent!.Value)
            .FirstOrDefault();

        var legsForBestWorst = await LoadLegsMapAsync(
            new[] { best?.Id, worst?.Id }.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList(),
            cancellationToken);

        return new RealizedPnlSummaryDto(
            closedTrades,
            openTrades,
            winCount,
            loseCount,
            flatCount,
            winRate,
            avgReturn,
            medianReturn,
            eligible.Count > 0 ? Math.Round(totalWeighted, 2) : null,
            avgHolding,
            best is null ? null : ToRealizedTradeDto(best, legsForBestWorst),
            worst is null ? null : ToRealizedTradeDto(worst, legsForBestWorst),
            missingSellPriceCount,
            approximateCount,
            cfg.BuyFeePercent,
            cfg.SellFeePercent,
            cfg.SellTaxPercent,
            cfg.WinThresholdPercent,
            MethodologyNote);
    }

    public async Task<RealizedTradesResponseDto> GetRealizedTradesAsync(
        int days = 180,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var lookback = Math.Clamp(days, 1, 3650);
        limit = Math.Clamp(limit, 1, 500);
        var today = TradingCalendar.TodayVietnam();
        var fromDate = TradingSessionMath.SubtractTradingSessions(today, lookback);

        var positions = await masterPositions.GetPositionsSinceAsync(fromDate, cancellationToken);
        var closed = positions
            .Where(p => p.IsClosed)
            .OrderByDescending(p => p.ClosedDate)
            .Take(limit)
            .ToList();

        var legsMap = await LoadLegsMapAsync(closed.Select(p => p.Id).ToList(), cancellationToken);
        var trades = closed.Select(p => ToRealizedTradeDto(p, legsMap)).ToList();

        return new RealizedTradesResponseDto(lookback, fromDate, trades.Count, trades);
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<PositionSellLegRecord>>> LoadLegsMapAsync(
        IReadOnlyList<Guid> positionIds,
        CancellationToken cancellationToken)
    {
        if (positionIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<PositionSellLegRecord>>();

        var legs = await masterPositions.GetSellLegsAsync(positionIds, cancellationToken);
        return legs
            .GroupBy(l => l.PositionId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PositionSellLegRecord>)g.ToList());
    }

    private static RealizedTradeDto ToRealizedTradeDto(
        MasterAlertPositionRecord p,
        IReadOnlyDictionary<Guid, IReadOnlyList<PositionSellLegRecord>> legsMap)
    {
        legsMap.TryGetValue(p.Id, out var legs);
        var sell1 = legs?.FirstOrDefault(l => l.Signal == MasterAlertKinds.SellPoint1Half);
        var sellAll = legs?.FirstOrDefault(l => l.Signal == MasterAlertKinds.SellAll);

        return new RealizedTradeDto(
            p.Id,
            p.Symbol,
            p.EntryDate,
            p.EntryPrice,
            p.ClosedDate,
            p.MaxPositionSize,
            sell1?.SellPrice,
            sell1?.SellDate,
            sellAll?.SellPrice,
            sellAll?.SellDate,
            p.RealizedWeightedReturnPercent,
            p.RealizedReturnOnDeployedPercent,
            p.RealizedGrossReturnPercent,
            p.RealizedOutcomeBucket,
            p.RealizedStatus,
            p.HoldingSessions,
            p.MarketPhaseAtEntry,
            p.ExitRegime);
    }

    private static decimal Median(IReadOnlyList<decimal> sortedValues)
    {
        var n = sortedValues.Count;
        if (n % 2 == 1)
            return sortedValues[n / 2];

        return Math.Round((sortedValues[n / 2 - 1] + sortedValues[n / 2]) / 2m, 2);
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
