using StockRadar.Application.Abstractions;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Services;

public sealed class HitCalibrationService(
    ISetupTrackRepository tracks,
    IHitCalibrationRepository calibrationRepo)
{
    public async Task<HitCalibrationProfile> RebuildAsync(CancellationToken cancellationToken = default)
    {
        var measured = await tracks.GetMeasuredWithPredictionsAsync(cancellationToken);
        var samples = measured
            .Where(t => t.PredictedHitPercent is > 0)
            .Select(t => new SetupPredictionSample(
                t.PredictedHitPercent!.Value,
                string.Equals(t.OutcomeBucket, "Good", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var profile = HitCalibrationBuilder.Build(samples);
        var bias = HitCalibrationBuilder.ComputePredictionBias(samples);
        await calibrationRepo.SaveAsync(profile, bias, cancellationToken);
        return profile;
    }
}
