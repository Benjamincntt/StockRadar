using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockRadar.Application.Abstractions;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    ApplicationDbContext db,
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ISectorCatalogRepository sectors,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        await users.EnsureGuestUserAsync(cancellationToken);
        await users.EnsureAdminUserAsync(passwordHasher.Hash("123456aH"), cancellationToken);
        await sectors.EnsureSeededAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var stockCount = await db.Stocks.CountAsync(cancellationToken);
        if (stockCount == 0)
            logger.LogInformation("DB trống — chạy Job 1 (backfill lịch sử) trước khi dùng app.");
        else
            logger.LogInformation("Database ready ({StockCount} ma trong DB).", stockCount);
    }
}
