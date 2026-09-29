using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.Persistence;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Bộ chạy Pha 1 — Trước phiên (08:30 mỗi ngày T2-T6).
/// Nhiệm vụ: Sơ tuyển → đánh giá Bối cảnh + Hình thái → lưu state WATCHING/FORMING.
/// </summary>
internal sealed class Pha1TruocPhienRunner(
    ISoTuyenService soTuyen,
    IMayNhanKichBan mayNhanKichBan,
    IJobStockRepository stockRepo,
    ApplicationDbContext db,
    ILogger<Pha1TruocPhienRunner> logger) : IPha1TruocPhienService
{
    /// <summary>Số phiên lịch sử tối thiểu để chỉ báo đáng tin (khớp ngưỡng sơ tuyển).</summary>
    private const int SoPhienToiThieu = 250;

    public async Task<Pha1KetQua> ChayAsync(CancellationToken ct = default)
    {
        // 1. Sơ tuyển — lọc ~1500 mã xuống tập đáng quan tâm.
        var soTuyenResult = await soTuyen.ChaySoTuyenAsync(ct);
        logger.LogInformation(
            "Sơ tuyển hoàn tất: {Count} mã đạt / {Total} mã đầu vào.",
            soTuyenResult.DanhSachMa.Count,
            soTuyenResult.TongSoMaDauVao);

        // 2. Với mỗi mã qua sơ tuyển, lấy history và đánh giá Bối cảnh + Hình thái.
        var tatCaKetQua = new List<KetQuaKichBan>();
        foreach (var symbol in soTuyenResult.DanhSachMa)
        {
            ct.ThrowIfCancellationRequested();

            var history = await LayLichSuAsync(symbol, ct);
            if (history is null || history.Count < SoPhienToiThieu)
            {
                logger.LogDebug("Bỏ qua {Symbol} — lịch sử {Count} phiên < {Min}.", symbol, history?.Count ?? 0, SoPhienToiThieu);
                continue;
            }

            var ketQua = await mayNhanKichBan.DanhGiaTruocPhienAsync(symbol, history, ct);
            tatCaKetQua.AddRange(ketQua);
        }

        // 3. Lưu DB (upsert theo Symbol + LoaiKichBan + NgayDanhGia).
        var ngayDanhGia = VietnamMarketCalendar.TodayVietnam().ToDateTime(TimeOnly.MinValue);
        await LuuKetQuaAsync(tatCaKetQua, ngayDanhGia, ct);

        // 4. Thống kê.
        var forming = tatCaKetQua.Count(k => k.TrangThai == TrangThaiKichBan.DangHinhThanh);
        var watching = tatCaKetQua.Count(k => k.TrangThai == TrangThaiKichBan.DangTheoDoi);
        logger.LogInformation("Pha 1 hoàn tất: {Forming} FORMING, {Watching} WATCHING.", forming, watching);

        return new Pha1KetQua(soTuyenResult, tatCaKetQua, forming, watching);
    }

    /// <summary>Lấy lịch sử OHLCV của một mã (null nếu không có).</summary>
    private async Task<IReadOnlyList<OhlcvBar>?> LayLichSuAsync(string symbol, CancellationToken ct)
    {
        var stock = await stockRepo.GetBySymbolAsync(symbol, ct);
        return stock?.History;
    }

    /// <summary>
    /// Upsert kết quả vào bảng KetQuaKichBan theo khóa (Symbol, LoaiKichBan, NgayDanhGia).
    /// </summary>
    private async Task LuuKetQuaAsync(
        IReadOnlyList<KetQuaKichBan> ketQua,
        DateTime ngayDanhGia,
        CancellationToken ct)
    {
        if (ketQua.Count == 0)
            return;

        var symbols = ketQua.Select(k => k.Symbol).Distinct().ToList();
        var hienTai = await db.KetQuaKichBan
            .Where(e => e.NgayDanhGia == ngayDanhGia && symbols.Contains(e.Symbol))
            .ToListAsync(ct);
        var lookup = hienTai.ToDictionary(e => (e.Symbol, e.LoaiKichBan));

        foreach (var k in ketQua)
        {
            var keHoachJson = k.KeHoach is null ? null : JsonSerializer.Serialize(k.KeHoach);
            var bangChupJson = k.BangChup is null ? null : JsonSerializer.Serialize(k.BangChup);
            var bangChungJson = JsonSerializer.Serialize(k.DanhSachBangChung);

            if (lookup.TryGetValue((k.Symbol, k.LoaiKichBan), out var entity))
            {
                entity.TrangThai = k.TrangThai;
                entity.DatBoiCanh = k.DatBoiCanh;
                entity.DatHinhThai = k.DatHinhThai;
                entity.DatCoKichHoat = k.DatCoKichHoat;
                entity.MucHoanThien = k.MucHoanThien;
                entity.ThoiGianKichHoat = k.ThoiGianKichHoat;
                entity.DiemXepHang = k.DiemXepHang;
                entity.KeHoachGiaoDichJson = keHoachJson;
                entity.BangChupChiBaoJson = bangChupJson;
                entity.DanhSachBangChungJson = bangChungJson;
                entity.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                db.KetQuaKichBan.Add(new KetQuaKichBanEntity
                {
                    Symbol = k.Symbol,
                    LoaiKichBan = k.LoaiKichBan,
                    TrangThai = k.TrangThai,
                    DatBoiCanh = k.DatBoiCanh,
                    DatHinhThai = k.DatHinhThai,
                    DatCoKichHoat = k.DatCoKichHoat,
                    MucHoanThien = k.MucHoanThien,
                    NgayDanhGia = ngayDanhGia,
                    ThoiGianKichHoat = k.ThoiGianKichHoat,
                    KeHoachGiaoDichJson = keHoachJson,
                    BangChupChiBaoJson = bangChupJson,
                    DanhSachBangChungJson = bangChungJson,
                    DiemXepHang = k.DiemXepHang,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
