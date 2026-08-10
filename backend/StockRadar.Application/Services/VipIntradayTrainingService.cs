using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Services;
using StockRadar.Domain.Services.OpportunityRanking;

namespace StockRadar.Application.Services;

public sealed class VipIntradayTrainingService(
    IVipAlertFireRepository fires,
    IVipIntradayRankerModelStore modelStore,
    IVipIntradayRanker ranker,
    IVipIntradayCalibrationService calibration,
    IVipIntradayThresholdService thresholds,
    IOptions<MasterAlertOptions> options,
    IOptions<OpportunityPerformanceOptions> performanceOptions) : IVipIntradayTrainingService
{
    public async Task<OpportunityRankerTrainingResultDto> TrainAndSaveAsync(
        int days = 90,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        var lookback = Math.Clamp(days > 0 ? days : cfg.IntradayDefaultDatasetDays, 14, 365);
        var today = TradingCalendar.TodayVietnam();
        var from = TradingSessionMath.SubtractTradingSessions(today, lookback);
        var successThreshold = performanceOptions.Value.SuccessThresholdPercent;
        var minSamples = Math.Max(10, cfg.IntradayMinSamplesToTrain);

        var rows = await fires.GetSinceAsync(from, cancellationToken);
        var labeled = rows
            .Where(r => r.IntradayMeasured && r.IntradayReturnPercent.HasValue)
            .Select(r =>
            {
                var input = ToInput(r);
                var label = r.IntradayReturnPercent!.Value >= successThreshold;
                return (Features: VipIntradayFeatures.Vectorize(input), Label: label, r.SessionDate, r.Symbol);
            })
            .OrderBy(x => x.SessionDate)
            .ThenBy(x => x.Symbol)
            .ToList();

        if (labeled.Count < minSamples)
        {
            return new OpportunityRankerTrainingResultDto(
                false,
                labeled.Count,
                0,
                0,
                null,
                cfg.IntradayModelPath,
                $"Cần ≥{minSamples} fire đã đo intraday — hiện có {labeled.Count}. Thu thập thêm rồi train lại.");
        }

        var holdoutCount = Math.Max(1, labeled.Count / 5);
        var trainCount = labeled.Count - holdoutCount;
        if (trainCount < Math.Min(24, minSamples))
        {
            trainCount = labeled.Count;
            holdoutCount = 0;
        }

        var trainSamples = labeled.Take(trainCount)
            .Select(x => (x.Features, x.Label))
            .ToList();
        var result = LogisticRegressionTrainer.Train(
            trainSamples,
            cfg.IntradayTrainingEpochs,
            featureNames: VipIntradayFeatures.Names,
            minSamples: Math.Min(minSamples, trainSamples.Count));

        if (!result.Model.IsTrained)
        {
            return new OpportunityRankerTrainingResultDto(
                false,
                result.Samples,
                result.Accuracy,
                result.PositiveRate,
                null,
                cfg.IntradayModelPath,
                "Huấn luyện intraday thất bại.");
        }

        decimal promoteMetric = result.Accuracy;
        var modelToSave = result.Model;
        var labelNote = $"in-sample AUC {promoteMetric:0.#}%";
        if (holdoutCount > 0)
        {
            var holdout = labeled.Skip(trainCount)
                .Select(x => (x.Features, x.Label))
                .ToList();
            promoteMetric = LogisticRegressionTrainer.ComputeAuc(
                result.Model.Intercept, result.Model.Weights, holdout);
            modelToSave = new OpportunityRankerModel
            {
                Intercept = result.Model.Intercept,
                Weights = result.Model.Weights,
                FeatureNames = result.Model.FeatureNames,
                TrainingSamples = result.Model.TrainingSamples,
                TrainingAccuracy = promoteMetric,
                TrainedAtUtc = result.Model.TrainedAtUtc,
                Version = result.Model.Version,
            };
            labelNote = $"holdout AUC {promoteMetric:0.#}% (train {result.Accuracy:0.#}% / {trainCount}, holdout {holdoutCount})";
        }

        await modelStore.SaveAsync(modelToSave, cancellationToken);
        await ranker.ReloadModelAsync(cancellationToken);

        // Phase 4: rebuild calibration + dynamic thresholds từ cùng dataset.
        if (cfg.IntradayCalibrationEnabled)
            await calibration.RebuildAsync(cancellationToken);
        if (cfg.DynamicThresholdEnabled)
            await thresholds.RefreshFromKpiAsync(cancellationToken);

        return new OpportunityRankerTrainingResultDto(
            true,
            result.Samples,
            promoteMetric,
            result.PositiveRate,
            modelToSave.TrainedAtUtc,
            cfg.IntradayModelPath,
            $"Đã train VipIntraday — {labelNote}.");
    }

    private static VipIntradayInput ToInput(VipAlertFireRecord r) =>
        new(
            r.GainFromOpenPercent,
            r.PacedVolumeRatio,
            r.MlProbAtFire ?? r.PredictedHitPercent ?? 50m,
            r.AtrPercent,
            r.DistMa20Percent,
            r.UptrendLong,
            r.ForeignNet,
            r.PropNet,
            r.SessionPressure,
            IsVsaXa: string.Equals(r.VsaLabel, TradeEventLabels.Xa, StringComparison.OrdinalIgnoreCase));
}
