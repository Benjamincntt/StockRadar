using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Constants;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.Persistence;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Bộ chạy Pha 3 — chấm điểm theo CHU KỲ (C-27, Phase 9b).
/// P1: chỉ chấm dòng có chu kỳ đóng (ThoiGianThoatHet != null); đang mở → KetQuaDoLuong = null ("Chờ đo").
/// P2: giá vào = giá khớp điểm 1 (bình quân hai lần mua khi có điểm 2);
///     giá ra = bình quân trọng số các lần bán (bán nửa + bán hết) hoặc giá bán hết.
/// P3: dòng còn mở mang kết quả cũ (di sản cách đo 4 ngày) → xoá về null, chờ đóng.
/// Phân loại Thắng/Thua/Ngang theo ngưỡng ±1% so giá vào; giữ hành vi đổi TrangThai theo kết quả đo.
/// </summary>
internal sealed class Pha3DoLuongRunner(
    ApplicationDbContext db,
    ILogger<Pha3DoLuongRunner> logger) : IPha3DoLuongService
{
    /// <summary>Ngưỡng % lợi nhuận để phân loại Thắng/Thua (giữ nguyên như cách đo cũ).</summary>
    private const decimal NguongPhanTram = 1m;

    /// <summary>Kết quả của các dòng bị huỷ — không bao giờ được chấm hay xoá (C-24).</summary>
    private const string KetQuaHuy = "Huy";

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

            // P2 (C-27/C-4): giá vào = giá khớp; có điểm 2 → bình quân hai lần mua.
            // Kế hoạch/cột chưa có giá khớp (dòng cũ) → biên dưới vùng vào (Q6).
            var giaVaoDiem1 = entity.GiaVaoThucTe ?? keHoach.GiaVaoLenhMin;
            var giaVao = entity.GiaMuaDiem2ThucTe is { } giaDiem2
                ? (giaVaoDiem1 + giaDiem2) / 2m
                : giaVaoDiem1;

            // P2: giá ra = bình quân trọng số các lần bán. Bán hết bắt buộc có giá khi chu kỳ đóng;
            // thiếu (dữ liệu bất thường) → bỏ qua, không tự bịa giá.
            if (entity.GiaThoatHet is null)
            {
                logger.LogWarning("Pha 3 — bỏ qua {Symbol}, chu kỳ đóng nhưng thiếu GiaThoatHet.", entity.Symbol);
                continue;
            }
            var giaThoat = entity.GiaBanNua is { } giaNua
                ? (giaNua + entity.GiaThoatHet.Value) / 2m
                : entity.GiaThoatHet.Value;

            var pTram = (giaThoat - giaVao) / giaVao * 100m;
            string ketQua;
            if (pTram >= NguongPhanTram)
            {
                ketQua = "Thang";
                entity.TrangThai = TrangThaiKichBan.ChotLoi;
            }
            else if (pTram <= -NguongPhanTram)
            {
                ketQua = "Thua";
                entity.TrangThai = TrangThaiKichBan.HuyLenh;
            }
            else
            {
                ketQua = "Ngang";
            }

            // % lợi nhuận thực tế của cả chu kỳ (luôn lưu để đối chiếu).
            entity.PhanTramLoiNhuan = Math.Round(pTram, 4);

            // R:R thực tế = (giá ra − giá vào) / (giá vào − SL); bỏ qua nếu giá vào == SL.
            decimal? tyLeLaiLo = null;
            var ruiRo = giaVao - keHoach.GiaDungLo;
            if (ruiRo != 0)
                tyLeLaiLo = Math.Round((giaThoat - giaVao) / ruiRo, 4);

            entity.GiaThoat = giaThoat;
            entity.NgayThoat = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(entity.ThoiGianThoatHet!.Value, DateTimeKind.Utc), TimeZoneVN));
            entity.TyLeLaiLoThucTe = tyLeLaiLo;
            entity.KetQuaDoLuong = ketQua;
            entity.UpdatedAt = DateTime.UtcNow;

            tongDoLuong++;
            switch (ketQua)
            {
                case "Thang": thang++; break;
                case "Thua": thua++; break;
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
