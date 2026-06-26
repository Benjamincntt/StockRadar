using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Infrastructure.Persistence.Entities;

namespace StockRadar.Infrastructure.Persistence.Repositories;

internal sealed class EfDailyOpportunityRepository(ApplicationDbContext db) : IDailyOpportunityRepository
{
    public async Task ReplaceForDateAsync(
        DateOnly forTradingDate,
        IReadOnlyList<DailyOpportunityRecord> items,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.DailyOpportunities
            .Where(o => o.ForTradingDate == forTradingDate)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
            db.DailyOpportunities.RemoveRange(existing);

        if (items.Count > 0)
        {
            db.DailyOpportunities.AddRange(items.Select(r => new DailyOpportunityEntity
            {
                ForTradingDate = r.ForTradingDate,
                Rank = r.Rank,
                Symbol = r.Symbol,
                Name = r.Name,
                Sector = r.Sector,
                Score = r.Score,
                Price = r.Price,
                ChangePercent = r.ChangePercent,
                VolumeRatio = r.VolumeRatio,
                GeneratedAt = r.GeneratedAt
            }));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DailyOpportunityRecord>> GetForDateAsync(
        DateOnly forTradingDate,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.DailyOpportunities.AsNoTracking()
            .Where(o => o.ForTradingDate == forTradingDate)
            .OrderBy(o => o.Rank)
            .ToListAsync(cancellationToken);

        return rows.Select(ToRecord).ToList();
    }

    public async Task<DateOnly?> GetLatestForDateAsync(CancellationToken cancellationToken = default)
    {
        return await db.DailyOpportunities.AsNoTracking()
            .MaxAsync(o => (DateOnly?)o.ForTradingDate, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetSymbolsForDateAsync(
        DateOnly forTradingDate,
        CancellationToken cancellationToken = default)
    {
        return await db.DailyOpportunities.AsNoTracking()
            .Where(o => o.ForTradingDate == forTradingDate)
            .OrderBy(o => o.Rank)
            .Select(o => o.Symbol)
            .ToListAsync(cancellationToken);
    }

    private static DailyOpportunityRecord ToRecord(DailyOpportunityEntity e) =>
        new(
            e.ForTradingDate,
            e.Rank,
            e.Symbol,
            e.Name,
            e.Sector,
            e.Score,
            e.Price,
            e.ChangePercent,
            e.VolumeRatio,
            e.GeneratedAt);
}
