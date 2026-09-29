using System.Data.Common;
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

    public async Task<IReadOnlyList<string>> GetInactiveSymbolsAsync(CancellationToken cancellationToken = default) =>
        await db.Stocks.AsNoTracking()
            .Where(s => !s.IsActive && !s.TradingRestricted)
            .OrderBy(s => s.Symbol)
            .Select(s => s.Symbol)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Stock>> GetAllForUniverseScreeningAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await db.Stocks.AsNoTracking()
            .Where(s => !s.TradingRestricted)
            .ToListAsync(cancellationToken);
        return entities.Select(EntityMapper.ToDomain).ToList();
    }

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

    // Thuần cột — không đụng HistoryJson (LastClose/LastVolume do EfMarketDataWriter.ApplyHistory duy trì).
    private const string BreadthSql = """
        SELECT
            COALESCE(SUM(CASE WHEN LastChangePercent > 0 THEN 1 ELSE 0 END), 0),
            COALESCE(SUM(CASE WHEN LastChangePercent < 0 THEN 1 ELSE 0 END), 0),
            COALESCE(SUM(CASE WHEN LastChangePercent = 0 THEN 1 ELSE 0 END), 0),
            COALESCE(SUM(LastVolume), 0),
            COALESCE(SUM(LastClose * LastVolume), 0)
        FROM Stocks
        WHERE IsActive = 1 AND TradingRestricted = 0
        """;

    public async Task<MarketBreadthStats> GetBreadthStatsAsync(CancellationToken cancellationToken = default)
    {
        var conn = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = BreadthSql;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new MarketBreadthStats(
                    reader.GetInt32(0),
                    reader.GetInt32(1),
                    reader.GetInt32(2),
                    reader.IsDBNull(3) ? 0L : reader.GetInt64(3),
                    reader.IsDBNull(4) ? 0m : reader.GetDecimal(4));
            }

            return MarketBreadthStats.Empty;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
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

/// <summary>Quản lý danh sách theo dõi (bản thân danh sách) của user hiện tại.</summary>
internal sealed class EfWatchlistListRepository(
    ApplicationDbContext db,
    ICurrentUserService currentUser) : IWatchlistListRepository
{
    public async Task<IReadOnlyList<WatchlistRow>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        return await db.Watchlists.AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderBy(w => w.ThuTu)
            .ThenBy(w => w.Id)
            .Select(w => new WatchlistRow(
                w.Id,
                w.UserId,
                w.Name,
                w.LaDanhSachNganh,
                w.MaNganh,
                w.ThuTu,
                w.LaMacDinh,
                w.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<WatchlistRow?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        return await db.Watchlists.AsNoTracking()
            .Where(w => w.Id == id && w.UserId == userId)
            .Select(w => new WatchlistRow(
                w.Id,
                w.UserId,
                w.Name,
                w.LaDanhSachNganh,
                w.MaNganh,
                w.ThuTu,
                w.LaMacDinh,
                w.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<WatchlistRow> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;

        // Đặt sau các danh sách hiện có (mặc định ThuTu=0, ngành 1..N) — custom luôn nằm cuối.
        var maxThuTu = await db.Watchlists
            .Where(w => w.UserId == userId)
            .MaxAsync(w => (int?)w.ThuTu, cancellationToken) ?? 0;

        var entity = new Entities.WatchlistEntity
        {
            UserId = userId,
            Name = name.Trim(),
            LaDanhSachNganh = false,
            MaNganh = null,
            ThuTu = maxThuTu + 1,
            LaMacDinh = false,
            CreatedAt = DateTime.UtcNow
        };

        db.Watchlists.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToRow(entity);
    }

    public async Task<bool> RenameAsync(int id, string newName, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        var entity = await db.Watchlists.FirstOrDefaultAsync(
            w => w.Id == id && w.UserId == userId,
            cancellationToken);

        if (entity is null)
            return false;

        entity.Name = newName.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        var entity = await db.Watchlists.FirstOrDefaultAsync(
            w => w.Id == id && w.UserId == userId,
            cancellationToken);

        if (entity is null)
            return false;

        // Items xóa theo cascade (FK WatchlistId).
        db.Watchlists.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<WatchlistRow> GetOrCreateDefaultAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        var existing = await db.Watchlists.AsNoTracking()
            .Where(w => w.UserId == userId && w.LaMacDinh)
            .Select(w => new WatchlistRow(
                w.Id,
                w.UserId,
                w.Name,
                w.LaDanhSachNganh,
                w.MaNganh,
                w.ThuTu,
                w.LaMacDinh,
                w.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
            return existing;

        var entity = new Entities.WatchlistEntity
        {
            UserId = userId,
            Name = "Mặc định",
            LaDanhSachNganh = false,
            MaNganh = null,
            ThuTu = 0,
            LaMacDinh = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Watchlists.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToRow(entity);
    }

    public async Task EnsureSectorWatchlistsAsync(
        IReadOnlyList<string> sectors,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        var existing = await db.Watchlists.AsNoTracking()
            .Where(w => w.UserId == userId && w.LaDanhSachNganh)
            .Select(w => w.MaNganh!)
            .ToListAsync(cancellationToken);

        var missing = sectors
            .Where(s => !existing.Contains(s, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (missing.Count == 0)
            return;

        // ThuTu = vị trí trong danh mục (1-based) — nằm sau danh sách mặc định (ThuTu = 0).
        for (var i = 0; i < sectors.Count; i++)
        {
            if (!missing.Contains(sectors[i], StringComparer.OrdinalIgnoreCase))
                continue;

            db.Watchlists.Add(new Entities.WatchlistEntity
            {
                UserId = userId,
                Name = sectors[i],
                LaDanhSachNganh = true,
                MaNganh = sectors[i],
                ThuTu = i + 1,
                LaMacDinh = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, int>> GetItemCountsAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        var counts = await db.WatchlistItems.AsNoTracking()
            .Where(w => w.Watchlist!.UserId == userId)
            .GroupBy(w => w.WatchlistId)
            .Select(g => new { WatchlistId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(g => g.WatchlistId, g => g.Count);
    }

    public async Task<IReadOnlyDictionary<string, int>> GetActiveSectorStockCountsAsync(
        CancellationToken cancellationToken = default)
    {
        var counts = await db.Stocks.AsNoTracking()
            .Where(s => s.IsActive && !s.TradingRestricted && s.Sector != "")
            .GroupBy(s => s.Sector)
            .Select(g => new { Sector = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // So khớp MaNganh không phân biệt hoa/thường — giống collation mặc định của SQL Server.
        return new Dictionary<string, int>(
            counts.ToDictionary(g => g.Sector, g => g.Count),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<string>> GetSectorSymbolsAsync(
        string maNganh,
        CancellationToken cancellationToken = default) =>
        await db.Stocks.AsNoTracking()
            .Where(s => s.Sector == maNganh && s.IsActive && !s.TradingRestricted)
            .OrderBy(s => s.Symbol)
            .Select(s => s.Symbol)
            .ToListAsync(cancellationToken);

    private static WatchlistRow ToRow(Entities.WatchlistEntity entity) =>
        new(
            entity.Id,
            entity.UserId,
            entity.Name,
            entity.LaDanhSachNganh,
            entity.MaNganh,
            entity.ThuTu,
            entity.LaMacDinh,
            entity.CreatedAt);
}

/// <summary>Quản lý mã trong một danh sách theo dõi cụ thể của user hiện tại.</summary>
internal sealed class EfWatchlistRepository(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IWatchlistListRepository watchlists) : IWatchlistRepository
{
    public async Task<IReadOnlyList<string>> GetSymbolsAsync(int watchlistId, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        return await db.WatchlistItems.AsNoTracking()
            .Where(w => w.WatchlistId == watchlistId && w.Watchlist!.UserId == userId)
            .OrderByDescending(w => w.AddedAt)
            .Select(w => w.Symbol)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(int watchlistId, string symbol, CancellationToken cancellationToken = default)
    {
        var normalized = symbol.ToUpperInvariant();
        var userId = currentUser.UserId;

        // Chỉ thêm vào danh sách thuộc quyền user hiện tại.
        var owned = await db.Watchlists.AnyAsync(
            w => w.Id == watchlistId && w.UserId == userId,
            cancellationToken);
        if (!owned)
            return;

        var exists = await db.WatchlistItems.AnyAsync(
            w => w.WatchlistId == watchlistId && w.Symbol == normalized,
            cancellationToken);
        if (exists)
            return;

        db.WatchlistItems.Add(new Entities.WatchlistItemEntity
        {
            WatchlistId = watchlistId,
            Symbol = normalized,
            AddedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(int watchlistId, string symbol, CancellationToken cancellationToken = default)
    {
        var normalized = symbol.ToUpperInvariant();
        var userId = currentUser.UserId;
        var item = await db.WatchlistItems.FirstOrDefaultAsync(
            w => w.WatchlistId == watchlistId
                && w.Symbol == normalized
                && w.Watchlist!.UserId == userId,
            cancellationToken);

        if (item is null)
            return;

        db.WatchlistItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ContainsAsync(int watchlistId, string symbol, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        return await db.WatchlistItems.AsNoTracking().AnyAsync(
            w => w.WatchlistId == watchlistId
                && w.Symbol == symbol.ToUpperInvariant()
                && w.Watchlist!.UserId == userId,
            cancellationToken);
    }

    // ==== Backward compat — thao tác trên danh sách mặc định của user hiện tại ====

    public async Task<IReadOnlyList<string>> GetSymbolsAsync(CancellationToken cancellationToken = default)
    {
        var def = await watchlists.GetOrCreateDefaultAsync(cancellationToken);
        return await GetSymbolsAsync(def.Id, cancellationToken);
    }

    public async Task AddAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var def = await watchlists.GetOrCreateDefaultAsync(cancellationToken);
        await AddAsync(def.Id, symbol, cancellationToken);
    }

    public async Task RemoveAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var def = await watchlists.GetOrCreateDefaultAsync(cancellationToken);
        await RemoveAsync(def.Id, symbol, cancellationToken);
    }

    public async Task<bool> ContainsAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var def = await watchlists.GetOrCreateDefaultAsync(cancellationToken);
        return await ContainsAsync(def.Id, symbol, cancellationToken);
    }
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
