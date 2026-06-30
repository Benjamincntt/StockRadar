using StockRadar.Application.Abstractions;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Services;

public sealed class AdaptiveScoringProfileFactory(ICriterionScoringRepository criterionRepo)
{
    public async Task<AdaptiveScoringProfile> LoadAsync(CancellationToken cancellationToken = default)
    {
        var details = await criterionRepo.GetWeightDetailsAsync(cancellationToken);
        return details.Count > 0
            ? AdaptiveScoringProfile.FromWeightDetails(details)
            : AdaptiveScoringProfile.Default;
    }
}
