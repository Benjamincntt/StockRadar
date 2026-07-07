using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Services.OpportunityRanking;

namespace StockRadar.Application.Services;

public sealed class OpportunityRankerTrainingService(
    IOpportunityRankingDatasetService datasetBuilder,
    IOpportunityRankerModelStore modelStore,
    IOpportunityRanker ranker,
    IOptions<OpportunityRankerOptions> options) : IOpportunityRankerTrainingService
{
    public async Task<OpportunityRankerTrainingResultDto> TrainAndSaveAsync(
        int days = 180,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        var dataset = await datasetBuilder.BuildAsync(days, cancellationToken);

        if (dataset.RowCount < 30)
        {
            return new OpportunityRankerTrainingResultDto(
                false,
                dataset.RowCount,
                0,
                dataset.PositiveRatePercent,
                null,
                cfg.ModelPath,
                $"Cần ≥30 mẫu đã đo T+2.5 — hiện có {dataset.RowCount}.");
        }

        var samples = dataset.Rows
            .Select(r =>
            {
                var input = new OpportunityRankInput(
                    r.BuyScore,
                    r.PredictedHitPercent,
                    r.SectorRank,
                    r.RelativeStrength5d,
                    r.VolumeRatio,
                    r.IsActionable ? Domain.Enums.StockTradeState.Actionable : Domain.Enums.StockTradeState.AwaitingTrigger,
                    r.SetupDna);
                return (OpportunityRankFeatures.Vectorize(input), r.LabelHit);
            })
            .ToList();

        var result = LogisticRegressionTrainer.Train(
            samples,
            cfg.TrainingEpochs);

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

        await modelStore.SaveAsync(result.Model, cancellationToken);
        await ranker.ReloadModelAsync(cancellationToken);

        return new OpportunityRankerTrainingResultDto(
            true,
            result.Samples,
            result.Accuracy,
            result.PositiveRate,
            result.Model.TrainedAtUtc,
            cfg.ModelPath,
            $"Đã train logistic regression — accuracy {result.Accuracy:0.#}% trên {result.Samples} mẫu.");
    }
}
