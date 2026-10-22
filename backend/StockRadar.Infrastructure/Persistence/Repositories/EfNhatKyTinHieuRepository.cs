using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.Enums;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Infrastructure.Persistence.Entities;

namespace StockRadar.Infrastructure.Persistence.Repositories;

/// <summary>
/// Picker tín hiệu cho nhật ký (N1/Q2).
/// LIMIT: chỉ SELECT từ KetQuaKichBan/MasterAlertPositions/PositionSellLegs — cấm ghi, không đụng runner V1/V2 (FR-007).
/// </summary>
public sealed class EfNhatKyTinHieuRepository(ApplicationDbContext db) : INhatKyTinHieuRepository
{
    public async Task<(List<TinHieuV2Row> Items, int Total)> LayTinHieuV2Async(
        DateTime? tuUtc, DateTime? denUtc, int page, int size, CancellationToken ct = default)
    {
        var query = db.KetQuaKichBan
            .Where(x => x.TrangThai >= TrangThaiKichBan.DaKichHoat && x.ThoiGianKichHoat != null);
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

    public async Task<(List<TinHieuVipRow> Items, int Total)> LayTinHieuVipAsync(
        DateTime? tuUtc, DateTime? denUtc, int page, int size, CancellationToken ct = default)
    {
        // Ngày filter quy về DateOnly TRƯỚC khi vào query — EntryDate là DateOnly.
        var tu = tuUtc.HasValue ? DateOnly.FromDateTime(tuUtc.Value) : (DateOnly?)null;
        var den = denUtc.HasValue ? DateOnly.FromDateTime(denUtc.Value) : (DateOnly?)null;

        var query = db.MasterAlertPositions.AsQueryable();
        if (tu.HasValue)
            query = query.Where(x => x.EntryDate >= tu.Value);
        if (den.HasValue)
            query = query.Where(x => x.EntryDate <= den.Value);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.EntryDate)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .AsNoTracking()
            .ToListAsync(ct);

        return (rows.Select(x => new TinHieuVipRow(
            x.Id, x.Symbol, x.EntryDate, x.EntryPrice, x.IsClosed, x.RealizedReturnOnDeployedPercent)).ToList(), total);
    }

    public async Task<TinHieuVipRow?> LayMotTinHieuVipAsync(Guid id, CancellationToken ct = default)
    {
        var x = await db.MasterAlertPositions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (x is null)
            return null;

        // N8: giá bán nhịp đầu/nhịp sau của VIP nằm ở PositionSellLegs (BanNua | BanHet).
        var legs = await db.PositionSellLegs.AsNoTracking()
            .Where(l => l.PositionId == id)
            .OrderBy(l => l.SellDate).ThenBy(l => l.CreatedAt)
            .ToListAsync(ct);

        return new TinHieuVipRow(
            x.Id, x.Symbol, x.EntryDate, x.EntryPrice, x.IsClosed, x.RealizedReturnOnDeployedPercent,
            legs.FirstOrDefault(l => l.Signal == MasterAlertKinds.SellPoint1Half)?.SellPrice,
            legs.FirstOrDefault(l => l.Signal == MasterAlertKinds.SellAll)?.SellPrice);
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

    public async Task<List<KetQuaVipRow>> LayKetQuaVipAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return [];
        return await db.MasterAlertPositions.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new KetQuaVipRow(x.Id, x.RealizedReturnOnDeployedPercent))
            .ToListAsync(ct);
    }

    private static TinHieuV2Row ToV2Row(Domain.Entities.KetQuaKichBanEntity x) => new(
        x.Id, x.Symbol, x.LoaiKichBan.ToString(), x.NgayDanhGia, x.ThoiGianKichHoat,
        x.GiaVaoThucTe, x.GiaMuaDiem2ThucTe, x.GiaBanNua, x.GiaThoatHet,
        x.KetQuaDoLuong, x.KeHoachGiaoDichJson);
}
