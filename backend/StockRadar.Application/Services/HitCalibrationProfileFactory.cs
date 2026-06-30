using StockRadar.Application.Abstractions;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Services;

public sealed class HitCalibrationProfileFactory(IHitCalibrationRepository calibrationRepo)
{
    public Task<HitCalibrationProfile> LoadAsync(CancellationToken cancellationToken = default) =>
        calibrationRepo.LoadAsync(cancellationToken);
}
