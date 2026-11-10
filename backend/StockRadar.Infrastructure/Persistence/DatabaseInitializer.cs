using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Infrastructure.Persistence.Repositories;

namespace StockRadar.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    ApplicationDbContext db,
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ISectorCatalogRepository sectors,
    IOptions<AdminOptions> adminOptions,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        await users.EnsureGuestUserAsync(cancellationToken);
        // SECURITY: mật khẩu admin không còn viết cứng trong repo. Đặt Admin:Password trên server;
        // đổi mật khẩu = sửa cấu hình rồi khởi động lại. Để trống thì không tạo/đổi admin.
        if (ShouldSetAdminPassword(adminOptions.Value.Password))
            await users.EnsureAdminUserAsync(passwordHasher.Hash(adminOptions.Value.Password), cancellationToken);
        else
            logger.LogWarning("chưa cấu hình Admin:Password — không tạo/đổi mật khẩu admin.");
        await sectors.EnsureSeededAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var stockCount = await db.Stocks.CountAsync(cancellationToken);
        if (stockCount == 0)
            logger.LogInformation("DB trống — chạy Job 1 (backfill lịch sử) trước khi dùng app.");
        else
            logger.LogInformation("Database ready ({StockCount} ma trong DB).", stockCount);
    }

    /// <summary>
    /// Quyết định thuần có gọi EnsureAdminUserAsync hay không theo Admin:Password (khác rỗng thì có).
    /// </summary>
    public static bool ShouldSetAdminPassword(string? adminPassword) =>
        !string.IsNullOrWhiteSpace(adminPassword);
}
