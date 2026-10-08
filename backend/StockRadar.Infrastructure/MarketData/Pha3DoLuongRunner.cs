using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.Persistence;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Bộ chạy Pha 3 — Đo lường outcome (sau T+3 phiên kể từ khi kịch bản kích hoạt).
/// Nhiệm vụ: lấy giá đóng cửa phiên gần nhất, so với kế hoạch (Entry/SL/TP1) để phân loại
/// Thắng/Thua/Ngang, tính % lợi nhuận và R:R thực tế rồi lưu vào bản ghi kịch bản.
/// </summary>
internal sealed class Pha3DoLuongRunner(
    IJobStockRepository stockRepo,
    ApplicationDbContext db,
    ILogger<Pha3DoLuongRunner> logger) : IPha3DoLuongService
{
    /// <summary>Số ngày lịch tối thiểu sau kích hoạt để đo (T+3 phiên ≈ 4 ngày lịch).</summary>
    private const int SoNgayChoDo = 4;

    /// <summary>Ngưỡng % lợi nhuận để phân loại Thắng/Thua khi chưa chạm TP1/SL.</summary>
    private const decimal NguongPhanTram = 1m;

    public async Task<Pha3KetQuaDto> RunAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-SoNgayChoDo);

        // 1. Lấy các kịch bản đã kích hoạt, đủ thời gian chờ và chưa được đo.
        var entities = await db.KetQuaKichBan
            .Where(e => e.TrangThai == TrangThaiKichBan.DaKichHoat
                        && e.ThoiGianKichHoat != null
                        && e.ThoiGianKichHoat <= cutoff
                        && e.KetQuaDoLuong == null)
            .ToListAsync(cancellationToken);

        if (entities.Count == 0)
        {
            logger.LogDebug("Pha 3 — không có kịch bản nào đến hạn đo.");
            return new Pha3KetQuaDto(0, 0, 0, 0, DateTime.UtcNow);
        }

        // Cache nến cuối theo symbol để tránh truy vấn lặp lại cho nhiều kịch bản cùng mã.
        var nenCuoiCache = new Dictionary<string, OhlcvBar?>(StringComparer.OrdinalIgnoreCase);

        var thang = 0;
        var thua = 0;
        var ngang = 0;
        var tongDoLuong = 0;

        foreach (var entity in entities)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 2a. Lấy nến gần nhất (giá đóng cửa + ngày) của mã.
            var nenCuoi = await LayNenCuoiAsync(entity.Symbol, nenCuoiCache, cancellationToken);
            if (nenCuoi is null || nenCuoi.Close <= 0)
            {
                logger.LogDebug("Pha 3 — bỏ qua {Symbol}, không có nến/giá đóng cửa hợp lệ.", entity.Symbol);
                continue;
            }

            // 2b. Giải mã kế hoạch giao dịch để lấy Entry (GiaVaoLenhMin), SL, TP1.
            var keHoach = GiaiMaKeHoach(entity.KeHoachGiaoDichJson);
            if (keHoach is null || keHoach.GiaVaoLenhMin <= 0)
            {
                logger.LogDebug("Pha 3 — bỏ qua {Symbol}, kế hoạch giao dịch không hợp lệ.", entity.Symbol);
                continue;
            }

            var giaVao = keHoach.GiaVaoLenhMin;
            var dungLo = keHoach.GiaDungLo;
            var chotLoi1 = keHoach.GiaChotLoi1;

            // 2c. Tính giá thoát + phân loại.
            var coGiaThoatPha2 = entity.GiaBanNua.HasValue || entity.GiaThoatHet.HasValue;
            decimal giaThoat;
            string ketQua;

            if (coGiaThoatPha2)
            {
                giaThoat = entity.GiaBanNua.HasValue && entity.GiaThoatHet.HasValue
                    ? (entity.GiaBanNua.Value + entity.GiaThoatHet.Value) / 2m
                    : entity.GiaThoatHet.HasValue
                        ? entity.GiaThoatHet.Value
                        : (entity.GiaBanNua!.Value + nenCuoi.Close) / 2m;

                var pTram = (giaThoat - giaVao) / giaVao * 100m;
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
            }
            else
            {
                giaThoat = nenCuoi.Close;

                if (chotLoi1 > 0 && giaThoat >= chotLoi1)
                {
                    ketQua = "Thang";
                    entity.TrangThai = TrangThaiKichBan.ChotLoi;
                }
                else if (dungLo > 0 && giaThoat <= dungLo)
                {
                    ketQua = "Thua";
                    entity.TrangThai = TrangThaiKichBan.HuyLenh;
                }
                else
                {
                    var pTram = (giaThoat - giaVao) / giaVao * 100m;
                    if (pTram >= NguongPhanTram) ketQua = "Thang";
                    else if (pTram <= -NguongPhanTram) ketQua = "Thua";
                    else ketQua = "Ngang";
                }
            }

            // % lợi nhuận thực tế (luôn lưu để đối chiếu).
            var phanTramLoiNhuan = Math.Round((giaThoat - giaVao) / giaVao * 100m, 4);

            // 2d. R:R thực tế = (giá thoát − entry) / (entry − SL); bỏ qua nếu entry == SL.
            decimal? tyLeLaiLo = null;
            var ruiRo = giaVao - dungLo;
            if (ruiRo != 0)
                tyLeLaiLo = Math.Round((giaThoat - giaVao) / ruiRo, 4);

            // 2e. Cập nhật bản ghi.
            entity.GiaThoat = giaThoat;
            entity.NgayThoat = nenCuoi.Date;
            entity.PhanTramLoiNhuan = phanTramLoiNhuan;
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

        // 3. Lưu thay đổi.
        await db.SaveChangesAsync(cancellationToken);

        // 4. Log tổng kết.
        logger.LogInformation(
            "Pha 3 hoàn tất: đã đo {Total} kịch bản — {Thang} thắng, {Thua} thua, {Ngang} ngang.",
            tongDoLuong, thang, thua, ngang);

        // 5. Trả về DTO tổng kết.
        return new Pha3KetQuaDto(tongDoLuong, thang, thua, ngang, DateTime.UtcNow);
    }

    /// <summary>Lấy nến gần nhất của một mã (có cache theo symbol).</summary>
    private async Task<OhlcvBar?> LayNenCuoiAsync(
        string symbol,
        Dictionary<string, OhlcvBar?> cache,
        CancellationToken ct)
    {
        if (cache.TryGetValue(symbol, out var cached))
            return cached;

        var stock = await stockRepo.GetBySymbolAsync(symbol, ct);
        var nenCuoi = stock?.History is { Count: > 0 } history
            ? history.MaxBy(b => b.Date)
            : null;

        cache[symbol] = nenCuoi;
        return nenCuoi;
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
