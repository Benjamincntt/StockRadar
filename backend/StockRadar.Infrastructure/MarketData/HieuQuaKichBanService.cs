using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.Persistence;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Service truy vấn hiệu quả kịch bản V2 (Performance Tracking).
/// Đọc các bản ghi <see cref="KetQuaKichBanEntity"/> đã được Pha 3 đo lường outcome
/// và tổng hợp thành tóm tắt / lịch sử / chi tiết cho UI (mobile/web).
/// </summary>
/// <remarks>
/// Đặt ở tầng Infrastructure vì cần truy cập trực tiếp <see cref="ApplicationDbContext"/>
/// (tầng Application chỉ tham chiếu Domain, không tham chiếu Infrastructure).
/// Interface <see cref="IHieuQuaKichBanService"/> và DTO vẫn nằm ở Application.
/// </remarks>
public sealed class HieuQuaKichBanService(ApplicationDbContext db) : IHieuQuaKichBanService
{
    /// <summary>Kết quả đo "Thắng" (lưu không dấu trong DB).</summary>
    private const string KetQuaThang = "Thang";

    /// <summary>Kết quả đo "Thua" (lưu không dấu trong DB).</summary>
    private const string KetQuaThua = "Thua";

    /// <summary>Kết quả đo "Ngang" (lưu không dấu trong DB).</summary>
    private const string KetQuaNgang = "Ngang";

    /// <inheritdoc />
    public async Task<HieuQuaTomTatDto> GetTomTatAsync(string period, CancellationToken ct = default)
    {
        var cutoff = TinhCatMoc(period);

        // Chỉ lấy các kịch bản đã kích hoạt trở lên (DaKichHoat/DangGiu/ChotLoi/HuyLenh/ThoatLenh).
        var query = db.KetQuaKichBan.AsNoTracking()
            .Where(e => e.TrangThai >= TrangThaiKichBan.DaKichHoat);

        // Lọc theo thời điểm kích hoạt nếu có kỳ (period != "all").
        if (cutoff is not null)
            query = query.Where(e => e.ThoiGianKichHoat != null && e.ThoiGianKichHoat >= cutoff);

        // Chỉ tải các cột cần thiết để tổng hợp, giảm tải dữ liệu.
        var rows = await query
            .Select(e => new
            {
                e.LoaiKichBan,
                e.KetQuaDoLuong,
                e.TyLeLaiLoThucTe,
                e.PhanTramLoiNhuan
            })
            .ToListAsync(ct);

        var tongKichHoat = rows.Count;
        var thang = rows.Count(r => r.KetQuaDoLuong == KetQuaThang);
        var thua = rows.Count(r => r.KetQuaDoLuong == KetQuaThua);
        var ngang = rows.Count(r => r.KetQuaDoLuong == KetQuaNgang);

        // "Chờ đo" = đã kích hoạt nhưng chưa có kết quả đo (KetQuaDoLuong == null).
        var choDo = rows.Count(r => string.IsNullOrEmpty(r.KetQuaDoLuong));

        // Tỷ lệ thắng loại "Chờ đo" khỏi mẫu số.
        var daDo = thang + thua + ngang;
        var tyLeThang = daDo == 0 ? 0m : Math.Round(thang * 100m / daDo, 2);

        var tbRR = TrungBinh(rows.Select(r => r.TyLeLaiLoThucTe));
        var tbPhanTram = TrungBinh(rows.Select(r => r.PhanTramLoiNhuan));

        // Nhóm theo loại kịch bản, ánh xạ sang tên hiển thị tiếng Việt.
        var theoLoai = rows
            .GroupBy(r => r.LoaiKichBan)
            .Select(g =>
            {
                var gThang = g.Count(r => r.KetQuaDoLuong == KetQuaThang);
                var gThua = g.Count(r => r.KetQuaDoLuong == KetQuaThua);
                var gNgang = g.Count(r => r.KetQuaDoLuong == KetQuaNgang);
                var gDaDo = gThang + gThua + gNgang;
                return new LoaiKichBanStatsDto(
                    TenKichBan(g.Key),
                    g.Count(),
                    gThang,
                    gThua,
                    gNgang,
                    gDaDo == 0 ? 0m : Math.Round(gThang * 100m / gDaDo, 2),
                    TrungBinh(g.Select(r => r.TyLeLaiLoThucTe)));
            })
            .OrderByDescending(s => s.Tong)
            .ToList();

        return new HieuQuaTomTatDto(
            tongKichHoat,
            thang,
            thua,
            ngang,
            choDo,
            tyLeThang,
            tbRR,
            tbPhanTram,
            theoLoai);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<LichSuLenhDto> Items, int TotalCount)> GetLichSuAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var query = db.KetQuaKichBan.AsNoTracking()
            .Where(e => e.TrangThai >= TrangThaiKichBan.DaKichHoat);

        var totalCount = await query.CountAsync(ct);

        var entities = await query
            .OrderByDescending(e => e.ThoiGianKichHoat)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = entities.Select(ToLichSu).ToList();
        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<ChiTietLenhDto?> GetChiTietAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.KetQuaKichBan.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (entity is null)
            return null;

        // Giữ nguyên JSON thô để UI tự phân tích/hiển thị.
        return new ChiTietLenhDto(
            ToLichSu(entity),
            entity.DanhSachBangChungJson,
            entity.BangChupChiBaoJson,
            entity.KeHoachGiaoDichJson);
    }

    /// <summary>Ánh xạ một bản ghi entity sang DTO dòng lịch sử lệnh.</summary>
    private static LichSuLenhDto ToLichSu(KetQuaKichBanEntity e)
    {
        var keHoach = GiaiMaKeHoach(e.KeHoachGiaoDichJson);
        var ngayKichHoat = e.ThoiGianKichHoat is { } tg
            ? DateOnly.FromDateTime(tg)
            : DateOnly.FromDateTime(e.NgayDanhGia);

        return new LichSuLenhDto(
            (int)e.Id,
            e.Symbol,
            TenKichBan(e.LoaiKichBan),
            KetQuaHienThi(e.KetQuaDoLuong),
            keHoach?.GiaVaoLenhMin ?? 0m,
            e.GiaThoat,
            e.PhanTramLoiNhuan,
            e.TyLeLaiLoThucTe,
            ngayKichHoat,
            e.NgayThoat);
    }

    /// <summary>Chuyển kết quả đo (không dấu) sang nhãn hiển thị tiếng Việt có dấu.</summary>
    private static string KetQuaHienThi(string? ketQuaDoLuong) => ketQuaDoLuong switch
    {
        KetQuaThang => "Thắng",
        KetQuaThua => "Thua",
        KetQuaNgang => "Ngang",
        _ => "Chờ đo"
    };

    /// <summary>Ánh xạ enum loại kịch bản sang tên hiển thị tiếng Việt.</summary>
    private static string TenKichBan(LoaiKichBan loai) => loai switch
    {
        LoaiKichBan.NoHuongLen => "Nổ hướng lên",
        LoaiKichBan.HoiHoTro => "Hồi hỗ trợ",
        LoaiKichBan.QuetThanhKhoan => "Quét thanh khoản",
        LoaiKichBan.KietSuc => "Kiệt sức",
        LoaiKichBan.GayNen => "Gãy nền",
        _ => loai.ToString()
    };

    /// <summary>
    /// Tính mốc thời gian (UTC) theo kỳ. null nghĩa là không lọc ("all").
    /// </summary>
    private static DateTime? TinhCatMoc(string period)
    {
        var now = DateTime.UtcNow;
        return period?.Trim().ToLowerInvariant() switch
        {
            "week" => now.AddDays(-7),
            "month" => now.AddDays(-30),
            "quarter" => now.AddDays(-90),
            _ => null // "all" hoặc giá trị lạ → không lọc theo thời gian.
        };
    }

    /// <summary>Trung bình cộng của các giá trị khác null; 0 nếu rỗng.</summary>
    private static decimal TrungBinh(IEnumerable<decimal?> values)
    {
        var list = values.Where(v => v != null).Select(v => v!.Value).ToList();
        return list.Count == 0 ? 0m : Math.Round(list.Average(), 2);
    }

    /// <summary>Giải mã JSON kế hoạch giao dịch (null nếu rỗng hoặc không parse được).</summary>
    private static KeHoachGiaoDich? GiaiMaKeHoach(string? keHoachJson)
    {
        if (string.IsNullOrWhiteSpace(keHoachJson))
            return null;
        try
        {
            return JsonSerializer.Deserialize<KeHoachGiaoDich>(keHoachJson);
        }
        catch
        {
            return null;
        }
    }
}
