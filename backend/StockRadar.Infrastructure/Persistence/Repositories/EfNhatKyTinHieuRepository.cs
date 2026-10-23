using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.Persistence.Entities;

namespace StockRadar.Infrastructure.Persistence.Repositories;

/// <summary>
/// Picker tín hiệu cho nhật ký (N1/Q2).
/// LIMIT: chỉ SELECT từ KetQuaKichBan — cấm ghi, không đụng runner V1/V2 (FR-007).
/// </summary>
public sealed class EfNhatKyTinHieuRepository(ApplicationDbContext db) : INhatKyTinHieuRepository
{
    public async Task<(List<TinHieuV2Row> Items, int Total)> LayTinHieuV2Async(
        DateTime? tuUtc, DateTime? denUtc, int page, int size, CancellationToken ct = default)
    {
        var query = db.KetQuaKichBan
            .Where(x => x.TrangThai >= TrangThaiKichBan.DaKichHoat
                        && x.ThoiGianKichHoat != null
                        && x.KetQuaDoLuong != ChamKetQuaChuKy.Huy);
        if (tuUtc.HasValue)
            query = query.Where(x => x.ThoiGianKichHoat >= tuUtc.Value);
        if (denUtc.HasValue)
            query = query.Where(x => x.ThoiGianKichHoat <= denUtc.Value);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.ThoiGianKichHoat)
            .Skip((page - 1) * size)
            .Take(size)
            .AsNoTracking()
            .ToListAsync(ct);

        return (rows.Select(ToV2Row).ToList(), total);
    }

    public async Task<TinHieuV2Row?> LayMotTinHieuV2Async(long id, CancellationToken ct = default)
    {
        var row = await db.KetQuaKichBan.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TrangThai >= TrangThaiKichBan.DaKichHoat, ct);
        return row is null ? null : ToV2Row(row);
    }

    public async Task<List<KetQuaV2Row>> LayKetQuaV2Async(IReadOnlyList<long> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return [];
        return await db.KetQuaKichBan.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new KetQuaV2Row(x.Id, x.KetQuaDoLuong, x.PhanTramLoiNhuan))
            .ToListAsync(ct);
    }

    private static TinHieuV2Row ToV2Row(Domain.Entities.KetQuaKichBanEntity x) => new(
        x.Id, x.Symbol, x.LoaiKichBan.ToString(), x.NgayDanhGia, x.ThoiGianKichHoat,
        x.GiaVaoThucTe, x.GiaMuaDiem2ThucTe, x.GiaBanNua, x.GiaThoatHet,
        x.KetQuaDoLuong, x.KeHoachGiaoDichJson);
}
