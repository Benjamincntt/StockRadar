using StockRadar.Application.DTOs;
using StockRadar.Domain.Services.OpportunityRanking;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Abstractions;

public interface IVipIntradayRanker
{
    bool IsModelActive { get; }

    OpportunityRankerModel GetModelSnapshot();

    decimal PredictWinProbability(VipIntradayInput input);

    Task ReloadModelAsync(CancellationToken cancellationToken = default);
}

public interface IVipIntradayRankerModelStore
{
    Task<OpportunityRankerModel> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(OpportunityRankerModel model, CancellationToken cancellationToken = default);
}

public interface IVipIntradayTrainingService
{
    Task<OpportunityRankerTrainingResultDto> TrainAndSaveAsync(
        int days = 90,
        CancellationToken cancellationToken = default);
}

public interface IVipIntradayCalibrationService
{
    HitCalibrationProfile GetProfile();

    Task<HitCalibrationProfile> RebuildAsync(CancellationToken cancellationToken = default);

    Task ReloadAsync(CancellationToken cancellationToken = default);
}

public interface IVipIntradayThresholdService
{
    decimal ResolveMinMlProb(string marketPhase);

    Task RefreshFromKpiAsync(CancellationToken cancellationToken = default);
}
