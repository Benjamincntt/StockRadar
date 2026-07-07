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

        var result = TrainFromRows(dataset.Rows, cfg.TrainingEpochs);
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

        var current = ranker.GetModelSnapshot();
        var shouldPromote = promoteAlways
            || !current.IsTrained
            || result.Accuracy >= current.TrainingAccuracy
            || result.Accuracy >= cfg.MinAccuracyToPromote;

        if (!shouldPromote)
        {
            await modelStore.SaveVersionOnlyAsync(result.Model, cancellationToken);

            return new OpportunityRankerTrainingResultDto(
                false,
                result.Samples,
                result.Accuracy,
                result.PositiveRate,
                result.Model.TrainedAtUtc,
                cfg.ModelPath,
                $"Model mới {result.Accuracy:0.#}% < active {current.TrainingAccuracy:0.#}% — giữ bản cũ.");
        }

        await modelStore.SaveAsync(result.Model, cancellationToken);
        await ranker.ReloadModelAsync(cancellationToken);

        var mode = promoteAlways ? "manual" : "auto";

        return new OpportunityRankerTrainingResultDto(
            true,
            result.Samples,
            result.Accuracy,
            result.PositiveRate,
            result.Model.TrainedAtUtc,
            cfg.ModelPath,
            $"Đã train ({mode}) — accuracy {result.Accuracy:0.#}% trên {result.Samples} mẫu.");
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
}
