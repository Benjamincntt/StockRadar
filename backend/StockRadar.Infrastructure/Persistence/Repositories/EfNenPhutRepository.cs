using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.Entities;

namespace StockRadar.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core cho nến 1 phút: mỗi mã mỗi phiên một dòng, ghi lại thì thay dòng cũ.
/// Thiết kế bảng và khoá duy nhất: specs/017-intraday-minute-bars/plan.md (D1, D3).
/// </summary>
internal sealed class EfNenPhutRepository(ApplicationDbContext db) : INenPhutRepository
{
    public Task<NenPhutTheoPhienEntity?> GetNenTheoPhienAsync(
        string symbol, DateOnly ngayPhien, CancellationToken cancellationToken = default)
    {
        var ma = symbol.Trim().ToUpperInvariant();
        return db.NenPhutTheoPhien.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Symbol == ma && x.NgayPhien == ngayPhien, cancellationToken);
    }

    public Task<SoChatLuongNenPhutEntity?> GetChatLuongAsync(
        string symbol, DateOnly ngayPhien, CancellationToken cancellationToken = default)
    {
        var ma = symbol.Trim().ToUpperInvariant();
        return db.SoChatLuongNenPhut.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Symbol == ma && x.NgayPhien == ngayPhien, cancellationToken);
    }

    public async Task<IReadOnlyList<SoChatLuongNenPhutEntity>> GetChatLuongTheoPhienAsync(
        DateOnly ngayPhien, CancellationToken cancellationToken = default) =>
        await db.SoChatLuongNenPhut.AsNoTracking()
            .Where(x => x.NgayPhien == ngayPhien)
            .OrderBy(x => x.Symbol)
            .ToListAsync(cancellationToken);

    public async Task LuuNenTheoPhienAsync(
        NenPhutTheoPhienEntity dong, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dong);

        var ma = dong.Symbol.Trim().ToUpperInvariant();
        var cu = await db.NenPhutTheoPhien
            .FirstOrDefaultAsync(x => x.Symbol == ma && x.NgayPhien == dong.NgayPhien, cancellationToken);

        if (cu is null)
        {
            dong.Symbol = ma;
            db.NenPhutTheoPhien.Add(dong);
        }
        else
        {
            cu.SoNen = dong.SoNen;
            cu.Nguon = dong.Nguon;
            cu.DuLieuNen = dong.DuLieuNen;
            cu.ThoiDiemGhi = dong.ThoiDiemGhi;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LuuChatLuongAsync(
        SoChatLuongNenPhutEntity dong, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dong);

        var ma = dong.Symbol.Trim().ToUpperInvariant();
        var cu = await db.SoChatLuongNenPhut
            .FirstOrDefaultAsync(x => x.Symbol == ma && x.NgayPhien == dong.NgayPhien, cancellationToken);

        if (cu is null)
        {
            dong.Symbol = ma;
            db.SoChatLuongNenPhut.Add(dong);
        }
        else
        {
            cu.SoNenNhanDuoc = dong.SoNenNhanDuoc;
            cu.TrangThai = dong.TrangThai;
            cu.GioNenAtc = dong.GioNenAtc;
            cu.SoLanThu = dong.SoLanThu;
            cu.ThongDiepLoi = dong.ThongDiepLoi;
            cu.LanGhi = dong.LanGhi;
            cu.ThoiDiemGhi = dong.ThoiDiemGhi;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
