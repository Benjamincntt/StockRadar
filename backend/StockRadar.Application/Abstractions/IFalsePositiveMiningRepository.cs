using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Abstractions;

public interface IFalsePositiveMiningRepository
{
    Task<FalsePositiveMiningResult?> GetLatestAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(FalsePositiveMiningResult result, CancellationToken cancellationToken = default);
}
