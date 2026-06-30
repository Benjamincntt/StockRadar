using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Abstractions;

public interface IHitCalibrationRepository
{
    Task<HitCalibrationProfile> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        HitCalibrationProfile profile,
        decimal predictionBiasPercent,
        CancellationToken cancellationToken = default);

    Task<HitCalibrationMeta> GetMetaAsync(CancellationToken cancellationToken = default);
}

public sealed record HitCalibrationMeta(
    decimal GlobalFactor,
    int TotalSamples,
    decimal PredictionBiasPercent,
    DateTime? UpdatedAt);
