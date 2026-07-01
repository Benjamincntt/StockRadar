using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Domain.Entities;
using StockRadar.Infrastructure.Persistence.Mapping;

namespace StockRadar.Infrastructure.Persistence.Repositories;

internal sealed class EfStockRepository(ApplicationDbContext db) : IStockRepository, IJobStockRepository
{
    public async Task<IReadOnlyList<Stock>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await db.Stocks.AsNoTracking()
            .Where(s => s.IsActive && !s.TradingRestricted)
            .ToListAsync(cancellationToken);
        return entities.Select(EntityMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<string>> GetActiveSymbolsAsync(CancellationToken cancellationToken = default) =>
        await db.Stocks.AsNoTracking()
            .Where(s => s.IsActive && !s.TradingRestricted)
            .OrderBy(s => s.Symbol)
            .Select(s => s.Symbol)
            .ToListAsync(cancellationToken);

    public async Task<Stock?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var entity = await db.Stocks.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Symbol == symbol.ToUpperInvariant(), cancellationToken);
        return entity is null ? null : EntityMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<StockSummaryRow>> GetSummariesBySymbolsAsync(
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken = default)
    {
        if (symbols.Count == 0)
            return [];

        var normalized = symbols
            .Select(s => s.Trim().ToUpperInvariant())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return await db.Stocks.AsNoTracking()
            .Where(s => normalized.Contains(s.Symbol))
            .Select(s => new StockSummaryRow(
                s.Symbol,
                s.Name,
                s.Sector,
                s.SectorLocked,
                s.LastChangePercent))
            .ToListAsync(cancellationToken);
    }
}

internal sealed class EfAlertRepository(ApplicationDbContext db) : IAlertRepository
{
    public async Task<IReadOnlyList<Alert>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await db.Alerts.AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
        return entities.Select(EntityMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<Alert>> GetForSessionDateAsync(
        DateOnly sessionDate,
        int take,
        CancellationToken cancellationToken = default)
    {
        var startUtc = TradingCalendar.StartOfVietnamDayUtc(sessionDate);
        var endUtc = startUtc.AddDays(1);

        var entities = await db.Alerts.AsNoTracking()
            .Where(a => a.CreatedAt >= startUtc && a.CreatedAt < endUtc)
            .OrderByDescending(a => a.CreatedAt)
            .Take(Math.Max(take, 1))
            .ToListAsync(cancellationToken);

        return entities.Select(EntityMapper.ToDomain).ToList();
    }

    public async Task AddAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        db.Alerts.Add(EntityMapper.ToEntity(alert));
        await db.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class EfWatchlistRepository(
    ApplicationDbContext db,
    ICurrentUserService currentUser) : IWatchlistRepository
{
    public async Task<IReadOnlyList<string>> GetSymbolsAsync(CancellationToken cancellationToken = default)
    {
        return await db.WatchlistItems.AsNoTracking()
            .Where(w => w.UserId == currentUser.UserId)
            .OrderByDescending(w => w.AddedAt)
            .Select(w => w.Symbol)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var normalized = symbol.ToUpperInvariant();
        var exists = await db.WatchlistItems.AnyAsync(
            w => w.UserId == currentUser.UserId && w.Symbol == normalized,
            cancellationToken);

        if (exists)
            return;

        db.WatchlistItems.Add(new Entities.WatchlistItemEntity
        {
            UserId = currentUser.UserId,
            Symbol = normalized,
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var normalized = symbol.ToUpperInvariant();
        var item = await db.WatchlistItems.FirstOrDefaultAsync(
            w => w.UserId == currentUser.UserId && w.Symbol == normalized,
            cancellationToken);

        if (item is null)
            return;

        db.WatchlistItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ContainsAsync(string symbol, CancellationToken cancellationToken = default) =>
        await db.WatchlistItems.AsNoTracking().AnyAsync(
            w => w.UserId == currentUser.UserId && w.Symbol == symbol.ToUpperInvariant(),
            cancellationToken);
}

internal sealed class EfUserRepository(ApplicationDbContext db) : IUserRepository
{
    public static readonly Guid GuestUserId = GuestUser.Id;

    public async Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var entity = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        return entity is null ? null : EntityMapper.ToDomain(entity);
    }

    public async Task<UserAccount?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        return entity is null ? null : EntityMapper.ToDomain(entity);
    }

    public async Task<UserAccount> CreateAsync(
        string email,
        string passwordHash,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var entity = new Entities.UserEntity
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHash,
            DisplayName = displayName,
            IsGuest = false,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return EntityMapper.ToDomain(entity);
    }

    public async Task EnsureGuestUserAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Users.AnyAsync(u => u.Id == GuestUserId, cancellationToken))
            return;

        db.Users.Add(new Entities.UserEntity
        {
            Id = GuestUserId,
            Email = "guest@stockradar.local",
            PasswordHash = "",
            DisplayName = "Guest",
            IsGuest = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureAdminUserAsync(string passwordHash, CancellationToken cancellationToken = default)
    {
        var entity = await db.Users.FirstOrDefaultAsync(u => u.Email == AdminUser.Email, cancellationToken);
        if (entity is null)
        {
            db.Users.Add(new Entities.UserEntity
            {
                Id = AdminUser.Id,
                Email = AdminUser.Email,
                PasswordHash = passwordHash,
                DisplayName = AdminUser.DisplayName,
                IsGuest = false,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            entity.PasswordHash = passwordHash;
            entity.DisplayName = AdminUser.DisplayName;
            entity.IsGuest = false;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class EfMarketIndexRepository(ApplicationDbContext db)
{
    public async Task<MarketIndex?> GetAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var entity = await db.MarketIndices.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Symbol == symbol, cancellationToken);
        return entity is null ? null : EntityMapper.ToDomain(entity);
    }

    public async Task UpsertAsync(MarketIndex index, CancellationToken cancellationToken = default)
    {
        var entity = await db.MarketIndices.FirstOrDefaultAsync(m => m.Symbol == index.Symbol, cancellationToken);
        if (entity is null)
        {
            db.MarketIndices.Add(EntityMapper.ToEntity(index));
        }
        else
        {
            entity.Price = index.Price;
            entity.ChangePercent = index.ChangePercent;
            entity.Score = index.Score;
            entity.Trend = (int)index.Trend;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
