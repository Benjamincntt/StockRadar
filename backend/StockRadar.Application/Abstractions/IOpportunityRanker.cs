using StockRadar.Application.DTOs;
using StockRadar.Domain.Services.OpportunityRanking;

namespace StockRadar.Application.Abstractions;

public interface IOpportunityRanker
{
    decimal PredictWinProbability(OpportunityRankInput input);

    bool IsModelActive { get; }

    OpportunityRankerModel GetModelSnapshot();

    Task ReloadModelAsync(CancellationToken cancellationToken = default);
}

public interface IOpportunityRankerModelStore
{
    Task<OpportunityRankerModel> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(OpportunityRankerModel model, CancellationToken cancellationToken = default);
}

public interface IOpportunityRankingDatasetService
{
    Task<OpportunityRankingDatasetDto> BuildAsync(
        int days = 180,
        CancellationToken cancellationToken = default);

    string ToCsv(OpportunityRankingDatasetDto dataset);
}

public interface IOpportunityRankerTrainingService
{
    Task<OpportunityRankerTrainingResultDto> TrainAndSaveAsync(
        int days = 180,
        CancellationToken cancellationToken = default);
}
