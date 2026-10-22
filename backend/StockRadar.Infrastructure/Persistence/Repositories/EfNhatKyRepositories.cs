using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Infrastructure.Persistence.Entities;

namespace StockRadar.Infrastructure.Persistence.Repositories;

/// <summary>
/// SECURITY: mỗi method đều nhận userId và lọc WHERE UserId — không bao giờ trả dữ liệu user khác (N6/FR-009).
/// </summary>
public sealed class EfNhatKyRepository(ApplicationDbContext db) : INhatKyRepository
{
    public async Task<(List<NhatKyLenhRow> Items, int Total)> LayDanhSachAsync(
        Guid userId, string? symbol, DateTime? tuUtc, DateTime? denUtc, string? loaiLenh,
        int page, int size, CancellationToken ct = default)
    {
        var query = db.NhatKyGiaoDich.Where(x => x.UserId == userId);

        if (!string.IsNullOrWhiteSpace(symbol))
            query = query.Where(x => x.MaChungKhoan == symbol.Trim().ToUpperInvariant());
        if (tuUtc.HasValue)
            query = query.Where(x => x.ThoiDiemKhop >= tuUtc.Value);
        if (denUtc.HasValue)
            query = query.Where(x => x.ThoiDiemKhop <= denUtc.Value);
        if (!string.IsNullOrWhiteSpace(loaiLenh))
            query = query.Where(x => x.Chieu == loaiLenh);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.ThoiDiemKhop)
            .ThenByDescending(x => x.TaoNgay)
            .Skip((page - 1) * size)
            .Take(size)
            .AsNoTracking()
            .ToListAsync(ct);

        return (items.Select(ToRow).ToList(), total);
    }

    public async Task<NhatKyLenhRow?> LayMotAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var row = await db.NhatKyGiaoDich.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        return row is null ? null : ToRow(row);
    }

    public async Task<List<NhatKyLenhRow>> LayToanBoLenhAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await db.NhatKyGiaoDich.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.ThoiDiemKhop)
            .ThenBy(x => x.TaoNgay)
            .ToListAsync(ct);
        return rows.Select(ToRow).ToList();
    }

    public async Task ThemAsync(NhatKyLenhRow row, CancellationToken ct = default)
    {
        db.NhatKyGiaoDich.Add(new NhatKyGiaoDichEntity
        {
            Id = row.Id,
            UserId = row.UserId,
            MaChungKhoan = row.MaChungKhoan,
            Chieu = row.Chieu,
            ThoiDiemKhop = row.ThoiDiemKhop,
            GiaKhop = row.GiaKhop,
            KhoiLuong = row.KhoiLuong,
            Phi = row.Phi,
            NguonTinHieu = row.NguonTinHieu,
            MaTinHieu = row.MaTinHieu,
            GiaTinHieuChup = row.GiaTinHieuChup,
            GhiChu = row.GhiChu,
            LyDo = row.LyDo,
            KLieuChinh = row.KLieuChinh,
            VonDieuChinh = row.VonDieuChinh,
            TaoNgay = row.TaoNgay,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task SuaAsync(NhatKyLenhRow row, CancellationToken ct = default)
    {
        var entity = await db.NhatKyGiaoDich
            .FirstOrDefaultAsync(x => x.Id == row.Id && x.UserId == row.UserId, ct);
        if (entity is null) return;

        entity.MaChungKhoan = row.MaChungKhoan;
        entity.Chieu = row.Chieu;
        entity.ThoiDiemKhop = row.ThoiDiemKhop;
        entity.GiaKhop = row.GiaKhop;
        entity.KhoiLuong = row.KhoiLuong;
        entity.Phi = row.Phi;
        entity.NguonTinHieu = row.NguonTinHieu;
        entity.MaTinHieu = row.MaTinHieu;
        entity.GiaTinHieuChup = row.GiaTinHieuChup;
        entity.GhiChu = row.GhiChu;
        entity.LyDo = row.LyDo;
        entity.KLieuChinh = row.KLieuChinh;
        entity.VonDieuChinh = row.VonDieuChinh;
        entity.CapNhatNgay = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task XoaAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var entity = await db.NhatKyGiaoDich
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (entity is null) return;
        db.NhatKyGiaoDich.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task<NhatKyBoQuaRow?> LayBoQuaAsync(Guid userId, string nguon, string maTinHieu, CancellationToken ct = default)
    {
        var row = await db.NhatKyTinHieuBoQua.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Nguon == nguon && x.MaTinHieu == maTinHieu, ct);
        return row is null ? null : new NhatKyBoQuaRow(row.Id, row.UserId, row.Nguon, row.MaTinHieu, row.Symbol, row.LyDo, row.BoQuaNgay);
    }

    public async Task<List<NhatKyBoQuaRow>> LayBoQuaTheoNguonAsync(Guid userId, string nguon, CancellationToken ct = default)
    {
        var rows = await db.NhatKyTinHieuBoQua.AsNoTracking()
            .Where(x => x.UserId == userId && x.Nguon == nguon)
            .ToListAsync(ct);
        return rows.Select(x => new NhatKyBoQuaRow(x.Id, x.UserId, x.Nguon, x.MaTinHieu, x.Symbol, x.LyDo, x.BoQuaNgay)).ToList();
    }

    public async Task ThemBoQuaAsync(NhatKyBoQuaRow row, CancellationToken ct = default)
    {
        var existing = await db.NhatKyTinHieuBoQua
            .FirstOrDefaultAsync(x => x.UserId == row.UserId && x.Nguon == row.Nguon && x.MaTinHieu == row.MaTinHieu, ct);
        if (existing is not null)
        {
            existing.LyDo = row.LyDo;
            existing.Symbol = row.Symbol;
            existing.BoQuaNgay = row.BoQuaNgay;
        }
        else
        {
            db.NhatKyTinHieuBoQua.Add(new NhatKyTinHieuBoQuaEntity
            {
                Id = row.Id,
                UserId = row.UserId,
                Nguon = row.Nguon,
                MaTinHieu = row.MaTinHieu,
                Symbol = row.Symbol,
                LyDo = row.LyDo,
                BoQuaNgay = row.BoQuaNgay,
            });
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task XoaBoQuaAsync(Guid userId, string nguon, string maTinHieu, CancellationToken ct = default)
    {
        var entity = await db.NhatKyTinHieuBoQua
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Nguon == nguon && x.MaTinHieu == maTinHieu, ct);
        if (entity is null) return;
        db.NhatKyTinHieuBoQua.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    private static NhatKyLenhRow ToRow(NhatKyGiaoDichEntity x) => new(
        x.Id, x.UserId, x.MaChungKhoan, x.Chieu, x.ThoiDiemKhop, x.GiaKhop, x.KhoiLuong, x.Phi,
        x.NguonTinHieu, x.MaTinHieu, x.GiaTinHieuChup, x.GhiChu, x.LyDo,
        x.KLieuChinh, x.VonDieuChinh, x.TaoNgay);
}
