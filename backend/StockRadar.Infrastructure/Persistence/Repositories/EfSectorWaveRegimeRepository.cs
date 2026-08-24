using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.Persistence.Entities;

namespace StockRadar.Infrastructure.Persistence.Repositories;

internal sealed class EfSectorWaveRegimeRepository(ApplicationDbContext db) : ISectorWaveRegimeRepository
{
    public async Task UpsertAsync(SectorWaveRegimeState state, CancellationToken cancellationToken = default)
    {
        var entity = await db.SectorWaveRegimes
            .FirstOrDefaultAsync(
                x => x.Sector == state.Sector && x.TradingDate == state.TradingDate,
                cancellationToken);

        if (entity is null)
        {
            entity = new SectorWaveRegimeEntity { Sector = state.Sector, TradingDate = state.TradingDate };
            db.SectorWaveRegimes.Add(entity);
        }

        entity.IsActive = state.IsActive;
        entity.ActivatedOn = state.ActivatedOn;
        entity.SessionsSinceActivation = state.SessionsSinceActivation;
        entity.ConsecutiveLowVolumeSessions = state.ConsecutiveLowVolumeSessions;
        entity.FailedOn = state.FailedOn;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SectorWaveRegimeState?> GetLatestAsync(
        string sector,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.SectorWaveRegimes.AsNoTracking()
            .Where(x => x.Sector == sector)
            .OrderByDescending(x => x.TradingDate)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    private static SectorWaveRegimeState ToDomain(SectorWaveRegimeEntity e) => new(
        e.Sector,
        e.TradingDate,
        e.IsActive,
        e.ActivatedOn,
        e.SessionsSinceActivation,
        e.ConsecutiveLowVolumeSessions,
        e.FailedOn);
}
