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

    Task SaveVersionOnlyAsync(OpportunityRankerModel model, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpportunityRankerModelVersionInfo>> ListVersionsAsync(
        CancellationToken cancellationToken = default);

    Task<bool> RevertToVersionAsync(string versionFileName, CancellationToken cancellationToken = default);
}

public sealed record OpportunityRankerModelVersionInfo(
    string FileName,
    DateTime? TrainedAtUtc,
    int TrainingSamples,
    decimal TrainingAccuracy,
    bool IsActive);

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

    Task<OpportunityRankerTrainingResultDto> TryAutoRetrainAsync(
        CancellationToken cancellationToken = default);
}

public interface ISetupTrackBackfillService
{
    Task<SetupTrackBackfillResultDto> BackfillFromDailyOpportunitiesAsync(
        int days = 180,
        CancellationToken cancellationToken = default);
}
