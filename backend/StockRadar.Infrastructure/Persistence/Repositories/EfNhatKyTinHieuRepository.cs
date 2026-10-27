using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.Enums;
using StockRadar.Infrastructure.Persistence;

namespace StockRadar.Infrastructure.Persistence.Repositories;

/// <summary>
/// Picker tín hiệu cho nhật ký (FR-040).
/// LIMIT: chỉ SELECT từ <c>TinHieuV3</c> — cấm ghi, không còn đọc bảng kịch bản V2 (đã gỡ).
/// Chỉ hiện tín hiệu đã vào lệnh (có giá vào).
/// </summary>
public sealed class EfNhatKyTinHieuRepository(ApplicationDbContext db) : INhatKyTinHieuRepository
{
    public async Task<(List<TinHieuV3Row> Items, int Total)> LayTinHieuV3Async(
        DateTime? tuUtc, DateTime? denUtc, int page, int size, CancellationToken ct = default)
    {
        var query = db.TinHieuV3.Where(x => x.GiaVao != null);
        if (tuUtc.HasValue)
            query = query.Where(x => x.CreatedAt >= tuUtc.Value);
        if (denUtc.HasValue)
            query = query.Where(x => x.CreatedAt <= denUtc.Value);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .AsNoTracking()
            .ToListAsync(ct);

        return (rows.Select(ToRow).ToList(), total);
    }

    public async Task<TinHieuV3Row?> LayMotTinHieuV3Async(long id, CancellationToken ct = default)
    {
        var row = await db.TinHieuV3.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.GiaVao != null, ct);
        return row is null ? null : ToRow(row);
    }

    public async Task<List<KetQuaV3Row>> LayKetQuaV3Async(IReadOnlyList<long> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return [];
        return await db.TinHieuV3.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new KetQuaV3Row(x.Id, x.KetQua, x.PhanTramLoiNhuan))
            .ToListAsync(ct);
    }

    private static TinHieuV3Row ToRow(Domain.Entities.TinHieuV3Entity x) => new(
        x.Id, x.Symbol, x.Loai.ToString(), x.NgayTinHieu, x.GiaVao, x.GiaThoat, x.KetQua);
}
