using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Infrastructure.Persistence.Entities;

namespace StockRadar.Infrastructure.Persistence.Repositories;

/// <summary>Persists device registrations and ownership through the application context.</summary>
/// <param name="coSoDuLieu">Scoped persistence context.</param>
public sealed class EfThietBiNhanThongBaoRepository(ApplicationDbContext coSoDuLieu)
    : IThietBiNhanThongBaoRepository
{
    /// <inheritdoc />
    public async Task DangKyAsync(Guid userId, string token, string nenTang, string? tokenCu = null,
        CancellationToken cancellationToken = default)
    {
        var maMoi = token.Trim();
        var maCu = tokenCu?.Trim();
        var may = await coSoDuLieu.ThietBiNhanThongBao
            .SingleOrDefaultAsync(thietBi => thietBi.Token == maMoi, cancellationToken);
        var hienTai = DateTime.UtcNow;

        if (may is null)
        {
            may = new ThietBiNhanThongBaoEntity
            {
                Token = maMoi,
                NgayDangKy = hienTai
            };
            coSoDuLieu.ThietBiNhanThongBao.Add(may);
        }

        may.UserId = userId;
        may.NenTang = nenTang;
        may.LanCapNhatGanNhat = hienTai;

        // SECURITY: Without checking ownership, token rotation could remove another user's device and stop its notifications.
        if (!string.IsNullOrEmpty(maCu) && !string.Equals(maCu, maMoi, StringComparison.Ordinal))
        {
            var mayCu = await coSoDuLieu.ThietBiNhanThongBao
                .SingleOrDefaultAsync(thietBi => thietBi.Token == maCu && thietBi.UserId == userId,
                    cancellationToken);
            if (mayCu is not null)
                coSoDuLieu.ThietBiNhanThongBao.Remove(mayCu);
        }

        await coSoDuLieu.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task XoaAsync(string token, CancellationToken cancellationToken = default)
    {
        var maDangKy = token.Trim();
        var may = await coSoDuLieu.ThietBiNhanThongBao
            .SingleOrDefaultAsync(thietBi => thietBi.Token == maDangKy, cancellationToken);
        if (may is null)
            return;

        coSoDuLieu.ThietBiNhanThongBao.Remove(may);
        await coSoDuLieu.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task XoaCacTokenAsync(IReadOnlyList<string> tokens, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tokens.Count == 0)
            return;

        var cacMay = await coSoDuLieu.ThietBiNhanThongBao
            .Where(thietBi => tokens.Contains(thietBi.Token))
            .ToListAsync(cancellationToken);
        if (cacMay.Count == 0)
            return;

        coSoDuLieu.ThietBiNhanThongBao.RemoveRange(cacMay);
        await coSoDuLieu.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> LayTokenTheoUserAsync(Guid userId,
        CancellationToken cancellationToken = default) =>
        await coSoDuLieu.ThietBiNhanThongBao.AsNoTracking()
            .Where(thietBi => thietBi.UserId == userId)
            .Select(thietBi => thietBi.Token)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> LayTokenCacMayAsync(CancellationToken cancellationToken = default) =>
        await coSoDuLieu.ThietBiNhanThongBao.AsNoTracking()
            .Select(thietBi => thietBi.Token)
            .ToListAsync(cancellationToken);
}
