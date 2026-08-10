using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Abstractions;

public interface IVipIntradayCalibrationStore
{
    Task<HitCalibrationProfile?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(HitCalibrationProfile profile, CancellationToken cancellationToken = default);
}
