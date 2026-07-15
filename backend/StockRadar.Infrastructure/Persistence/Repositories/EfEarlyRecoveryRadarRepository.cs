using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Infrastructure.Persistence.Entities;

namespace StockRadar.Infrastructure.Persistence.Repositories;

internal sealed class EfEarlyRecoveryRadarRepository(ApplicationDbContext db) : IEarlyRecoveryRadarRepository
{
    public async Task ReplaceForDateAsync(
        DateOnly forTradingDate,
        IReadOnlyList<EarlyRecoveryRecord> items,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.EarlyRecoveryRadar
            .Where(o => o.ForTradingDate == forTradingDate)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
            db.EarlyRecoveryRadar.RemoveRange(existing);

        if (items.Count > 0)
        {
            db.EarlyRecoveryRadar.AddRange(items.Select(r => new EarlyRecoveryRadarEntity
            {
                ForTradingDate = r.ForTradingDate,
                Symbol = r.Symbol,
                Name = r.Name,
                Sector = r.Sector,
                Price = r.Price,
                ChangePercent = r.ChangePercent,
                VolumeRatio = r.VolumeRatio,
                Rs5 = r.Rs5,
                RsPercentile = r.RsPercentile,
                MarketPhase = r.MarketPhase,
                Reason = r.Reason,
                GeneratedAt = r.GeneratedAt,
            }));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EarlyRecoveryRecord>> GetForDateAsync(
        DateOnly forTradingDate,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.EarlyRecoveryRadar.AsNoTracking()
            .Where(o => o.ForTradingDate == forTradingDate)
            .OrderByDescending(o => o.RsPercentile)
            .ThenBy(o => o.Symbol)
            .ToListAsync(cancellationToken);

        return rows.Select(ToRecord).ToList();
    }

    public async Task<DateOnly?> GetLatestForDateAsync(CancellationToken cancellationToken = default)
    {
        return await db.EarlyRecoveryRadar.AsNoTracking()
            .MaxAsync(o => (DateOnly?)o.ForTradingDate, cancellationToken);
    }

    private static EarlyRecoveryRecord ToRecord(EarlyRecoveryRadarEntity e) =>
        new(
            e.ForTradingDate,
            e.Symbol,
            e.Name,
            e.Sector,
            e.Price,
            e.ChangePercent,
            e.VolumeRatio,
            e.Rs5,
            e.RsPercentile,
            e.MarketPhase,
            e.Reason,
            e.GeneratedAt);
}
