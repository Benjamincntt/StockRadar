using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Constants;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.Persistence;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Bộ chạy Pha 3 — chấm điểm theo CHU KỲ (C-27, Phase 9b).
/// P1: chỉ chấm dòng có chu kỳ đóng (ThoiGianThoatHet != null) và chưa có kết quả — C-33 khiến vai trò này
/// thành chấm BÙ: Pha 2 đã chấm ngay lượt bán hết, đây là lưới cho dòng đóng tay/lỗi giữa chừng.
/// P3: dòng còn mở mang kết quả cũ (di sản cách đo 4 ngày) → xoá về null, chờ đóng.
/// Công thức chấm dùng chung ChamKetQuaChuKy với Pha 2 — không có bản sao thứ hai ở đây.
/// </summary>
internal sealed class Pha3DoLuongRunner(
    ApplicationDbContext db,
    ILogger<Pha3DoLuongRunner> logger) : IPha3DoLuongService
{
    /// <summary>Kết quả của các dòng bị huỷ — không bao giờ được chấm hay xoá (C-24).</summary>
    private const string KetQuaHuy = ChamKetQuaChuKy.Huy;

    private static readonly TimeZoneInfo TimeZoneVN =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    public async Task<Pha3KetQuaDto> RunAsync(CancellationToken cancellationToken = default)
    {
        // BUSINESS-RULE: lệnh chờ vào chỉ sống hết phiên ngày đánh giá (Q4) — quên huỷ thì
        // dòng FORMING nằm lại, hôm sau Pha 2 vẫn xét và mua bằng kế hoạch của phiên trước.
        var ngayDanhGia = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);
        var hetHanVao = await db.KetQuaKichBan
            .Where(e => e.TrangThai == TrangThaiKichBan.DangHinhThanh && e.NgayDanhGia == ngayDanhGia)
            .ToListAsync(cancellationToken);
        foreach (var entity in hetHanVao)
        {
            entity.TrangThai = TrangThaiKichBan.HuyLenh;
            entity.KetQuaDoLuong = KetQuaHuy;
            entity.LyDoThoatHet = SuKienBan.HetHanVao;
            entity.UpdatedAt = DateTime.UtcNow;
        }

        if (hetHanVao.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Pha 3 — huỷ {Count} kịch bản đang hình thành quá hạn vào.", hetHanVao.Count);
        }

        // P3 (C-27): xoá kết quả cũ trên dòng CÒN MỞ — di sản cách đo 4 ngày chấm khi chưa thoát.
        // "Huy" không bị xoá (dòng huỷ vĩnh viễn không chấm).
        var dongMoConKetQuaCu = await db.KetQuaKichBan
            .Where(e => e.ThoiGianThoatHet == null
                        && e.KetQuaDoLuong != null
                        && e.KetQuaDoLuong != KetQuaHuy)
            .ToListAsync(cancellationToken);
        foreach (var entity in dongMoConKetQuaCu)
        {
            entity.KetQuaDoLuong = null;
            entity.UpdatedAt = DateTime.UtcNow;
        }
        if (dongMoConKetQuaCu.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Pha 3 — P3: xoá {Count} kết quả đo cũ trên dòng còn mở, chờ chu kỳ đóng.",
                dongMoConKetQuaCu.Count);
        }

        // P1 (C-27): chỉ chấm chu kỳ ĐÃ ĐÓNG và chưa có kết quả (không chấm lại "Huy" hay dòng đã chấm).
        var entities = await db.KetQuaKichBan
            .Where(e => e.TrangThai >= TrangThaiKichBan.DaKichHoat
                        && e.ThoiGianThoatHet != null
                        && e.KetQuaDoLuong == null)
            .ToListAsync(cancellationToken);

        if (entities.Count == 0)
        {
            logger.LogDebug("Pha 3 — không có chu kỳ đóng nào cần chấm.");
            return new Pha3KetQuaDto(0, 0, 0, 0, DateTime.UtcNow);
        }

        var thang = 0;
        var thua = 0;
        var ngang = 0;
        var tongDoLuong = 0;

        foreach (var entity in entities)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var keHoach = GiaiMaKeHoach(entity.KeHoachGiaoDichJson);
            if (keHoach is null || keHoach.GiaVaoLenhMin <= 0)
            {
                logger.LogDebug("Pha 3 — bỏ qua {Symbol}, kế hoạch giao dịch không hợp lệ.", entity.Symbol);
                continue;
            }

            // C-33: công thức chấm nằm ở ChamKetQuaChuKy (dùng chung với Pha 2) — thiếu dữ liệu bắt buộc thì bỏ qua.
            var ngayThoat = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(entity.ThoiGianThoatHet!.Value, DateTimeKind.Utc), TimeZoneVN));
            var kq = ChamKetQuaChuKy.Tinh(
                keHoach, entity.GiaVaoThucTe, entity.GiaMuaDiem2ThucTe,
                entity.GiaBanNua, entity.GiaThoatHet, ngayThoat);
            if (kq is null)
            {
                logger.LogWarning("Pha 3 — bỏ qua {Symbol}, chu kỳ đóng nhưng thiếu GiaThoatHet hoặc giá vào hợp lệ.", entity.Symbol);
                continue;
            }

            ChamKetQuaChuKy.GhiVaoDong(entity, kq.Value);
            entity.UpdatedAt = DateTime.UtcNow;

            tongDoLuong++;
            switch (kq.Value.Loai)
            {
                case ChamKetQuaChuKy.Thang: thang++; break;
                case ChamKetQuaChuKy.Thua: thua++; break;
                default: ngang++; break;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Pha 3 hoàn tất: chấm {Total} chu kỳ đóng — {Thang} thắng, {Thua} thua, {Ngang} ngang.",
            tongDoLuong, thang, thua, ngang);

        return new Pha3KetQuaDto(tongDoLuong, thang, thua, ngang, DateTime.UtcNow);
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
