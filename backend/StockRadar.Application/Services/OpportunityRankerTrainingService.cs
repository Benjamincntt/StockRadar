using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services.OpportunityRanking;

namespace StockRadar.Application.Services;

public sealed class OpportunityRankerTrainingService(
    IOpportunityRankingDatasetService datasetBuilder,
    IOpportunityRankerModelStore modelStore,
    IOpportunityRanker ranker,
    IOptions<OpportunityRankerOptions> options) : IOpportunityRankerTrainingService
{
    public Task<OpportunityRankerTrainingResultDto> TrainAndSaveAsync(
        int days = 180,
        CancellationToken cancellationToken = default) =>
        TrainInternalAsync(days, promoteAlways: true, cancellationToken);

    public async Task<OpportunityRankerTrainingResultDto> TryAutoRetrainAsync(
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        if (!cfg.AutoRetrainEnabled)
        {
            return new OpportunityRankerTrainingResultDto(
                false,
                0,
                0,
                0,
                null,
                cfg.ModelPath,
                "AutoRetrain tắt — bật OpportunityRanker:AutoRetrainEnabled sau train manual.");
        }

        return await TrainInternalAsync(
            cfg.DefaultDatasetDays,
            promoteAlways: false,
            cancellationToken);
    }

    private async Task<OpportunityRankerTrainingResultDto> TrainInternalAsync(
        int days,
        bool promoteAlways,
        CancellationToken cancellationToken)
    {
        var cfg = options.Value;
        var minSamples = promoteAlways ? 30 : cfg.MinSamplesForRetrain;
        var dataset = await datasetBuilder.BuildAsync(days, cancellationToken);

        if (dataset.RowCount < minSamples)
        {
            return new OpportunityRankerTrainingResultDto(
                false,
                dataset.RowCount,
                0,
                dataset.PositiveRatePercent,
                null,
                cfg.ModelPath,
                $"Cần ≥{minSamples} mẫu đã đo T+2.5 — hiện có {dataset.RowCount}.");
        }

        if (dataset.PositiveLabels < cfg.MinPositiveLabelsForRetrain)
        {
            return new OpportunityRankerTrainingResultDto(
                false,
                dataset.RowCount,
                0,
                dataset.PositiveRatePercent,
                null,
                cfg.ModelPath,
                $"Cần ≥{cfg.MinPositiveLabelsForRetrain} label hit — hiện có {dataset.PositiveLabels}.");
        }

        // Time-based holdout: train 80% cũ nhất, đo accuracy trên 20% mới nhất.
        var chronological = dataset.Rows.OrderBy(r => r.EntryDate).ThenBy(r => r.Symbol).ToList();
        var holdoutCount = Math.Max(1, chronological.Count / 5);
        var trainCount = chronological.Count - holdoutCount;
        if (trainCount < 24)
        {
            // Quá ít để tách — train full, accuracy in-sample (pre-cleanup / cold start).
            trainCount = chronological.Count;
            holdoutCount = 0;
        }

        var trainRows = chronological.Take(trainCount).ToList();
        var holdoutRows = holdoutCount > 0 ? chronological.Skip(trainCount).ToList() : [];

        var result = TrainFromRows(trainRows, cfg.TrainingEpochs);
        if (!result.Model.IsTrained)
        {
            return new OpportunityRankerTrainingResultDto(
                false,
                result.Samples,
                result.Accuracy,
                result.PositiveRate,
                null,
                cfg.ModelPath,
                "Huấn luyện thất bại — không đủ mẫu.");
        }

        decimal promoteAccuracy;
        OpportunityRankerModel modelToSave;
        string accuracyLabel;
        if (holdoutRows.Count > 0)
        {
            promoteAccuracy = EvaluateAuc(result.Model, holdoutRows);
            modelToSave = new OpportunityRankerModel
            {
                Intercept = result.Model.Intercept,
                Weights = result.Model.Weights,
                FeatureNames = result.Model.FeatureNames,
                TrainingSamples = result.Model.TrainingSamples,
                TrainingAccuracy = promoteAccuracy,
                TrainedAtUtc = result.Model.TrainedAtUtc,
                Version = result.Model.Version,
            };
            accuracyLabel = $"holdout {promoteAccuracy:0.#}% (train {result.Accuracy:0.#}% / {trainRows.Count} mẫu, holdout {holdoutRows.Count})";
        }
        else
        {
            promoteAccuracy = result.Accuracy;
            modelToSave = result.Model;
            accuracyLabel = $"in-sample {promoteAccuracy:0.#}% ({result.Samples} mẫu, chưa đủ tách holdout)";
        }

        var current = ranker.GetModelSnapshot();
        // Auto-retrain phải vượt CẢ HAI: sàn chất lượng (MinAccuracyToPromote) VÀ không tệ hơn
        // model đang chạy. Trước đây dùng OR nên model dưới sàn 55% vẫn có thể promote miễn
        // không tệ hơn bản cũ (cũng dưới sàn) — model tệ tự duy trì, không bao giờ đạt sàn thật.
        var shouldPromote = promoteAlways
            || !current.IsTrained
            || (promoteAccuracy >= cfg.MinAccuracyToPromote && promoteAccuracy >= current.TrainingAccuracy);

        if (!shouldPromote)
        {
            await modelStore.SaveVersionOnlyAsync(modelToSave, cancellationToken);

            return new OpportunityRankerTrainingResultDto(
                false,
                result.Samples,
                promoteAccuracy,
                result.PositiveRate,
                modelToSave.TrainedAtUtc,
                cfg.ModelPath,
                $"Model mới {accuracyLabel} < active {current.TrainingAccuracy:0.#}% — giữ bản cũ.");
        }

        await modelStore.SaveAsync(modelToSave, cancellationToken);
        await ranker.ReloadModelAsync(cancellationToken);

        var mode = promoteAlways ? "manual" : "auto";

        return new OpportunityRankerTrainingResultDto(
            true,
            result.Samples,
            promoteAccuracy,
            result.PositiveRate,
            modelToSave.TrainedAtUtc,
            cfg.ModelPath,
            $"Đã train ({mode}) — {accuracyLabel}.");
    }

    private static LogisticRegressionTrainer.TrainingResult TrainFromRows(
        IReadOnlyList<OpportunityRankingRowDto> rows,
        int epochs)
    {
        var samples = rows
            .Select(r => (OpportunityRankFeatures.Vectorize(BuildInput(r)), r.LabelHit))
            .ToList();

        return LogisticRegressionTrainer.Train(samples, epochs);
    }

    private static decimal EvaluateAuc(
        OpportunityRankerModel model,
        IReadOnlyList<OpportunityRankingRowDto> rows)
    {
        if (rows.Count == 0 || !model.IsTrained)
            return 0;

        var samples = rows
            .Select(r =>
            {
                var input = BuildInput(r);
                return (OpportunityRankFeatures.Vectorize(input), r.LabelHit);
            })
            .ToList();

        return LogisticRegressionTrainer.ComputeAuc(model.Intercept, model.Weights, samples);
    }

    private static OpportunityRankInput BuildInput(OpportunityRankingRowDto r) =>
        new(r.BuyScore,
            r.PredictedHitPercent,
            r.SectorRank,
            r.RelativeStrength5d,
            r.VolumeRatio,
            r.IsActionable ? StockTradeState.Actionable : StockTradeState.AwaitingTrigger,
            r.SetupDna,
            AtrPercent: r.AtrPercent,
            DistMa20Percent: r.DistMa20Percent);
}
