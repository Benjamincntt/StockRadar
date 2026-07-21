using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Services.ReversalBounce;
using StockRadar.Infrastructure.Persistence.Entities;

namespace StockRadar.Infrastructure.Persistence.Repositories;

internal sealed class EfMarketBreadthSnapshotRepository(ApplicationDbContext db)
    : IMarketBreadthSnapshotRepository
{
    public async Task UpsertAsync(MarketBreadthSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var entity = await db.MarketBreadthSnapshots
            .FirstOrDefaultAsync(x => x.TradingDate == snapshot.TradingDate, cancellationToken);

        if (entity is null)
        {
            entity = new MarketBreadthSnapshotEntity { TradingDate = snapshot.TradingDate };
            db.MarketBreadthSnapshots.Add(entity);
        }

        entity.UniverseCount = snapshot.UniverseCount;
        entity.PctAboveMa20 = snapshot.PctAboveMa20;
        entity.PctAboveMa50 = snapshot.PctAboveMa50;
        entity.PctNewLow20 = snapshot.PctNewLow20;
        entity.PctUp = snapshot.PctUp;
        entity.PctDown = snapshot.PctDown;
        entity.FloorCount = snapshot.FloorCount;
        entity.CeilingCount = snapshot.CeilingCount;
        entity.MedianReturnPercent = snapshot.MedianReturnPercent;
        entity.MedianTurnover = snapshot.MedianTurnover;
        entity.VnIndexDrawdownPercent = snapshot.VnIndexDrawdownPercent;
        entity.VnIndexDistanceToMa20Percent = snapshot.VnIndexDistanceToMa20Percent;
        entity.VnIndexAboveMa20 = snapshot.VnIndexAboveMa20;
        entity.VnIndexReclaimedMa20 = snapshot.VnIndexReclaimedMa20;
        entity.Regime = snapshot.Regime.ToString();
        entity.ImproveStreak = snapshot.ImproveStreak;
        entity.Version = ReversalBounceOptions.BreadthVersion;
        entity.GeneratedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<MarketBreadthSnapshot?> GetForDateAsync(
        DateOnly tradingDate,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.MarketBreadthSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TradingDate == tradingDate, cancellationToken);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<MarketBreadthSnapshot?> GetPreviousAsync(
        DateOnly beforeDate,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.MarketBreadthSnapshots.AsNoTracking()
            .Where(x => x.TradingDate < beforeDate)
            .OrderByDescending(x => x.TradingDate)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<MarketBreadthSnapshot?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        var entity = await db.MarketBreadthSnapshots.AsNoTracking()
            .OrderByDescending(x => x.TradingDate)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : ToDomain(entity);
    }

    private static MarketBreadthSnapshot ToDomain(MarketBreadthSnapshotEntity e) => new(
        e.TradingDate,
        e.UniverseCount,
        e.PctAboveMa20,
        e.PctAboveMa50,
        e.PctNewLow20,
        e.PctUp,
        e.PctDown,
        e.FloorCount,
        e.CeilingCount,
        e.MedianReturnPercent,
        e.MedianTurnover,
        e.VnIndexDrawdownPercent,
        e.VnIndexDistanceToMa20Percent,
        e.VnIndexAboveMa20,
        e.VnIndexReclaimedMa20,
        Enum.TryParse<MarketRegime>(e.Regime, out var regime) ? regime : MarketRegime.Normal,
        e.ImproveStreak);
}
