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
            promoteAccuracy = EvaluateAccuracy(result.Model, holdoutRows);
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
        var shouldPromote = promoteAlways
            || !current.IsTrained
            || promoteAccuracy >= current.TrainingAccuracy
            || promoteAccuracy >= cfg.MinAccuracyToPromote;

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
            .Select(r =>
            {
                var input = new OpportunityRankInput(
                    r.BuyScore,
                    r.PredictedHitPercent,
                    r.SectorRank,
                    r.RelativeStrength5d,
                    r.VolumeRatio,
                    r.IsActionable ? StockTradeState.Actionable : StockTradeState.AwaitingTrigger,
                    r.SetupDna);
                return (OpportunityRankFeatures.Vectorize(input), r.LabelHit);
            })
            .ToList();

        return LogisticRegressionTrainer.Train(samples, epochs);
    }

    private static decimal EvaluateAccuracy(
        OpportunityRankerModel model,
        IReadOnlyList<OpportunityRankingRowDto> rows)
    {
        if (rows.Count == 0 || !model.IsTrained)
            return 0;

        var correct = 0;
        foreach (var r in rows)
        {
            var input = new OpportunityRankInput(
                r.BuyScore,
                r.PredictedHitPercent,
                r.SectorRank,
                r.RelativeStrength5d,
                r.VolumeRatio,
                r.IsActionable ? StockTradeState.Actionable : StockTradeState.AwaitingTrigger,
                r.SetupDna);
            var features = OpportunityRankFeatures.Vectorize(input);
            var p = PredictRaw(model.Intercept, model.Weights, features);
            var pred = p >= 0.5;
            if (pred == r.LabelHit)
                correct++;
        }

        return Math.Round(100m * correct / rows.Count, 1);
    }

    private static double PredictRaw(double intercept, double[] weights, double[] x)
    {
        var z = intercept;
        for (var i = 0; i < weights.Length && i < x.Length; i++)
            z += weights[i] * x[i];
        return 1.0 / (1.0 + Math.Exp(-z));
    }
}
