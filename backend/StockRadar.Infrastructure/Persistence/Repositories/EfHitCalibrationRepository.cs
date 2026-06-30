using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.Persistence.Entities;

namespace StockRadar.Infrastructure.Persistence.Repositories;

internal sealed class EfHitCalibrationRepository(ApplicationDbContext db) : IHitCalibrationRepository
{
    public async Task<HitCalibrationProfile> LoadAsync(CancellationToken cancellationToken = default)
    {
        var state = await db.HitCalibrationStates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        var buckets = await db.HitCalibrationBuckets.AsNoTracking()
            .OrderBy(x => x.PredictedMin)
            .ToListAsync(cancellationToken);

        if (state is null || buckets.Count == 0)
            return HitCalibrationProfile.Default;

        var profileBuckets = buckets.Select(b => new HitCalibrationBucket(
            b.BucketId,
            b.PredictedMin,
            b.PredictedMax,
            b.SampleCount,
            b.GoodCount,
            b.PredictedMidPercent,
            b.ActualHitRatePercent,
            b.CalibrationFactor)).ToList();

        return new HitCalibrationProfile(profileBuckets, state.GlobalFactor, state.TotalSamples);
    }

    public async Task<HitCalibrationMeta> GetMetaAsync(CancellationToken cancellationToken = default)
    {
        var state = await db.HitCalibrationStates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (state is null)
            return new HitCalibrationMeta(1m, 0, 0m, null);

        return new HitCalibrationMeta(
            state.GlobalFactor,
            state.TotalSamples,
            state.PredictionBiasPercent,
            state.UpdatedAt);
    }

    public async Task SaveAsync(
        HitCalibrationProfile profile,
        decimal predictionBiasPercent,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.HitCalibrationBuckets.ToListAsync(cancellationToken);
        if (existing.Count > 0)
            db.HitCalibrationBuckets.RemoveRange(existing);

        var now = DateTime.UtcNow;
        foreach (var b in profile.Buckets)
        {
            db.HitCalibrationBuckets.Add(new HitCalibrationBucketEntity
            {
                BucketId = b.BucketId,
                PredictedMin = b.PredictedMin,
                PredictedMax = b.PredictedMax,
                SampleCount = b.SampleCount,
                GoodCount = b.GoodCount,
                PredictedMidPercent = b.PredictedMidPercent,
                ActualHitRatePercent = b.ActualHitRatePercent,
                CalibrationFactor = b.CalibrationFactor,
                UpdatedAt = now,
            });
        }

        var state = await db.HitCalibrationStates.FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (state is null)
        {
            db.HitCalibrationStates.Add(new HitCalibrationStateEntity
            {
                Id = 1,
                GlobalFactor = profile.GlobalFactor,
                TotalSamples = profile.TotalSamples,
                PredictionBiasPercent = predictionBiasPercent,
                UpdatedAt = now,
            });
        }
        else
        {
            state.GlobalFactor = profile.GlobalFactor;
            state.TotalSamples = profile.TotalSamples;
            state.PredictionBiasPercent = predictionBiasPercent;
            state.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
