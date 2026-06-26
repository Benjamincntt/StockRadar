using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.Constants;
using StockRadar.Infrastructure.Persistence.Caching;

namespace StockRadar.Infrastructure.Persistence.Repositories;

internal sealed class EfSectorCatalogRepository(ApplicationDbContext db) : ISectorCatalogRepository
{
    public async Task<IReadOnlyList<string>> GetActiveNamesAsync(CancellationToken cancellationToken = default) =>
        await db.SectorDefinitions.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Name)
            .Select(s => s.Name)
            .ToListAsync(cancellationToken);

    public async Task<bool> ExistsAsync(string sectorName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sectorName))
            return false;

        var trimmed = sectorName.Trim();
        return await db.SectorDefinitions.AsNoTracking()
            .AnyAsync(s => s.IsActive && s.Name == trimmed, cancellationToken);
    }

    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        if (await db.SectorDefinitions.AnyAsync(cancellationToken))
            return;

        var order = 0;
        foreach (var name in SectorCatalog.DefaultSectors)
        {
            db.SectorDefinitions.Add(new Entities.SectorDefinitionEntity
            {
                Name = name,
                SortOrder = order++,
                IsActive = true
            });
        }

        var existingSectors = await db.Stocks.AsNoTracking()
            .Where(s => s.Sector != "")
            .Select(s => s.Sector)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var sector in existingSectors
                     .Where(s => !SectorCatalog.DefaultSectors.Contains(s, StringComparer.OrdinalIgnoreCase))
                     .OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
        {
            db.SectorDefinitions.Add(new Entities.SectorDefinitionEntity
            {
                Name = sector.Trim(),
                SortOrder = order++,
                IsActive = true
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class EfStockSectorRepository(ApplicationDbContext db, IMemoryCache cache) : IStockSectorRepository
{
    public async Task<bool> UpdateSectorAsync(string symbol, string sector, CancellationToken cancellationToken = default)
    {
        var sym = symbol.Trim().ToUpperInvariant();
        var entity = await db.Stocks.FirstOrDefaultAsync(s => s.Symbol == sym, cancellationToken);
        if (entity is null)
            return false;

        entity.Sector = sector.Trim();
        entity.SectorLocked = true;
        await db.SaveChangesAsync(cancellationToken);
        CacheInvalidation.InvalidateMarketData(cache);
        return true;
    }
}
