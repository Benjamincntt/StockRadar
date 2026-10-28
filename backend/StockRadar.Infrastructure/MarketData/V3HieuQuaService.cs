using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Truy vấn hiệu quả V3 cho màn "Hiệu quả" web/mobile (Phụ lục H). CHỈ đọc ViTheV3/TinHieuV3,
/// không tính lại chỉ báo. Lãi ròng % đã lưu trên vị thế khi đóng (mirror sang tín hiệu).
/// </summary>
internal sealed class V3HieuQuaService(
    ApplicationDbContext db,
    IOptions<V3Options> options) : IV3HieuQuaService
{
    private static readonly LoaiKichBanV3[] TatCaKichBan =
        [LoaiKichBanV3.KB1, LoaiKichBanV3.KB2, LoaiKichBanV3.KB4, LoaiKichBanV3.N1, LoaiKichBanV3.N3];

    public async Task<HieuQuaTomTatV3Dto> LayTomTatAsync(
        DateOnly? tu, DateOnly? den, CancellationToken ct = default)
    {
        var dongQuery = db.ViTheV3.Where(v => v.TrangThai == TrangThaiViTheV3.DaThoat
                                              && v.PhanTramLoiNhuan != null);
        if (tu is not null) dongQuery = dongQuery.Where(v => v.NgayThoat >= tu);
        if (den is not null) dongQuery = dongQuery.Where(v => v.NgayThoat <= den);
        var dong = await dongQuery.ToListAsync(ct);

        var theoKichBan = TatCaKichBan
            .Select(loai => TomTatKichBan(loai, dong.Where(v => v.Loai == loai).ToList()))
            .ToList();

        var danhMuc = await TinhDanhMucAsync(ct);

        DateOnly? denNgay = dong.Count > 0
            ? dong.Max(v => v.NgayThoat ?? default)
            : null;

        return new HieuQuaTomTatV3Dto(theoKichBan, danhMuc, denNgay);
    }

    private static HieuQuaTheoKichBanV3Dto TomTatKichBan(LoaiKichBanV3 loai, List<ViTheV3Entity> dong)
    {
        int soDong = dong.Count;
        int thang = dong.Count(v => v.KetQua == "Thang");
        int thua = dong.Count(v => v.KetQua == "Thua");
        int hoaVon = dong.Count(v => v.KetQua == "HoaVon");
        var lai = dong.Where(v => (v.PhanTramLoiNhuan ?? 0m) > 0m).Select(v => v.PhanTramLoiNhuan ?? 0m).ToList();
        var lo = dong.Where(v => (v.PhanTramLoiNhuan ?? 0m) < 0m).Select(v => v.PhanTramLoiNhuan ?? 0m).ToList();
        decimal tyLe = (thang + thua) > 0 ? (decimal)thang / (thang + thua) * 100m : 0m;
        decimal tong = dong.Sum(v => v.PhanTramLoiNhuan ?? 0m);
        decimal teNhat = soDong > 0 ? dong.Min(v => v.PhanTramLoiNhuan ?? 0m) : 0m;
        decimal giuTB = soDong > 0 ? (decimal)dong.Average(v => v.SoPhienDaGi) : 0m;

        return new HieuQuaTheoKichBanV3Dto(
            loai, V3TelegramFormatter.TenKichBan(loai), soDong, thang, thua, hoaVon,
            Math.Round(tyLe, 1),
            Math.Round(lai.Count > 0 ? lai.Average() : 0m, 2),
            Math.Round(lo.Count > 0 ? lo.Average() : 0m, 2),
            Math.Round(tong, 2), Math.Round(teNhat, 2), Math.Round(giuTB, 1));
    }

    private async Task<HieuQuaDanhMucV3Dto> TinhDanhMucAsync(CancellationToken ct)
    {
        var all = await db.ViTheV3.ToListAsync(ct);
        var von0 = options.Value.VonBanDau;

        var tongMua = all.Where(v => v.TrangThai is TrangThaiViTheV3.DangGiu or TrangThaiViTheV3.DaThoat
                                     && v.GiaTriLenh != null)
            .Sum(v => v.GiaTriLenh ?? 0m);
        var thuVe = all.Where(v => v.TrangThai == TrangThaiViTheV3.DaThoat && v.GiaThoat != null && v.KhoiLuong != null)
            .Sum(v => (v.GiaThoat ?? 0m) * (v.KhoiLuong ?? 0m));
        var tienMat = Math.Max(0m, von0 - tongMua + thuVe);
        var giaTriGiu = all.Where(v => v.TrangThai == TrangThaiViTheV3.DangGiu)
            .Sum(v => (v.KhoiLuong ?? 0m) * (v.GiaVao ?? 0m));
        int soDangGiu = all.Count(v => v.TrangThai == TrangThaiViTheV3.DangGiu);

        // ĐáyDrawdown theo đường vốn realized (chỉ dùng lệnh đã đóng).
        var dongTheoNgay = all.Where(v => v.TrangThai == TrangThaiViTheV3.DaThoat && v.NgayThoat != null)
            .OrderBy(v => v.NgayThoat)
            .ToList();
        decimal equity = von0, peak = von0, maxDD = 0m;
        foreach (var v in dongTheoNgay)
        {
            var lai = (v.GiaThoat ?? 0m) - (v.GiaVao ?? 0m);
            equity += lai * (v.KhoiLuong ?? 0m);
            if (equity > peak) peak = equity;
            var dd = peak > 0 ? (peak - equity) / peak * 100m : 0m;
            if (dd > maxDD) maxDD = dd;
        }

        return new HieuQuaDanhMucV3Dto(
            Math.Round(tienMat + giaTriGiu, 0), Math.Round(tienMat, 0), soDangGiu, Math.Round(maxDD, 2));
    }

    public async Task<ViTheV3PagedDto> LayLichSuAsync(
        int page, int size, string? status, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 200);

        var q = db.ViTheV3.AsNoTracking().AsQueryable();
        q = status?.Trim().ToLowerInvariant() switch
        {
            "mo" => q.Where(v => v.TrangThai == TrangThaiViTheV3.ChoMua || v.TrangThai == TrangThaiViTheV3.DangGiu),
            "dong" or "closed" => q.Where(v => v.TrangThai == TrangThaiViTheV3.DaThoat),
            "huy" => q.Where(v => v.TrangThai == TrangThaiViTheV3.BiHuy),
            _ => q,
        };

        int total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(v => v.NgayTinHieu).ThenByDescending(v => v.Id)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);

        return new ViTheV3PagedDto(items.Select(ToDto).ToList(), total, page, size);
    }

    public async Task<IReadOnlyList<ViTheV3Dto>> LayViTheDangGiuAsync(CancellationToken ct = default)
    {
        var rows = await db.ViTheV3.AsNoTracking()
            .Where(v => v.TrangThai == TrangThaiViTheV3.ChoMua || v.TrangThai == TrangThaiViTheV3.DangGiu)
            .OrderBy(v => v.Loai).ThenByDescending(v => v.NgayTinHieu)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<TinHieuV3Dto>> LayTinHieuTheoSymbolAsync(
        string symbol, CancellationToken ct = default)
    {
        var rows = await db.TinHieuV3.AsNoTracking()
            .Where(t => t.Symbol == symbol)
            .OrderByDescending(t => t.NgayTinHieu)
            .Take(50)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    private static ViTheV3Dto ToDto(ViTheV3Entity v) => new(
        v.Id, v.Symbol, v.Loai, v.NgayTinHieu, v.NgayVao, v.GiaVao, v.GiaTriLenh,
        v.TrangThai, v.SoPhienDaGi, v.NgayThoat, v.GiaThoat, v.LyDoThoat, v.PhanTramLoiNhuan, v.KetQua,
        v.LaHoangLoanSau);

    private static TinHieuV3Dto ToDto(TinHieuV3Entity t) => new(
        t.Id, t.Symbol, t.Loai, t.NgayTinHieu, t.GiaTinHieu, t.Hang, t.GiaTriXepHang,
        t.MuaMoCuaPhienSau, t.TrangThai, t.SnapshotJson, t.GiaVao, t.GiaThoat, t.KetQua);
}
