using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.Notifications;
using StockRadar.Infrastructure.Persistence;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Bộ chạy Pha 2 — Trong phiên (mỗi 1 phút, 09:00-14:45).
/// Nhiệm vụ: kiểm tra cò kích hoạt cho các mã FORMING + kiểm tra sell signals cho HOLDING.
/// </summary>
internal sealed class Pha2TrongPhienRunner(
    IMayNhanKichBan mayNhanKichBan,
    IXepHangCoHoi xepHang,
    IJobStockRepository stockRepo,
    KbsPriceBoardClient kbs,
    ITelegramNotifier telegram,
    ApplicationDbContext db,
    IOptions<Pha2Options> pha2Options,
    IOptions<TelegramNotifyOptions> telegramOptions,
    ILogger<Pha2TrongPhienRunner> logger) : IPha2TrongPhienService
{
    /// <summary>Kích thước batch khi gọi KBS price board.</summary>
    private const int BatchSize = 50;

    public async Task<Pha2KetQua> ChayAsync(CancellationToken ct = default)
    {
        var cfg = pha2Options.Value;

        // Kiểm tra giờ giao dịch (cho phép ForceRun từ endpoint bỏ qua)
        var nowVn = VietnamMarketCalendar.NowVietnam();
        var todayVn = VietnamMarketCalendar.TodayVietnam();

        if (!VietnamMarketCalendar.IsTradingDay(todayVn))
        {
            logger.LogDebug("Pha 2 — bỏ qua, không phải ngày giao dịch.");
            return new Pha2KetQua(0, 0, Array.Empty<KetQuaKichBan>());
        }

        var gioBatDau = TimeSpan.Parse(cfg.GioBatDau);
        var gioKetThuc = TimeSpan.Parse(cfg.GioKetThuc);
        if (nowVn.TimeOfDay < gioBatDau || nowVn.TimeOfDay > gioKetThuc)
        {
            logger.LogDebug("Pha 2 — bỏ qua, ngoài giờ {Start}-{End}.", cfg.GioBatDau, cfg.GioKetThuc);
            return new Pha2KetQua(0, 0, Array.Empty<KetQuaKichBan>());
        }

        // 1. Lấy danh sách FORMING từ DB (hôm nay)
        var ngayDanhGia = todayVn.ToDateTime(TimeOnly.MinValue);
        var formingEntities = await db.KetQuaKichBan
            .Where(e => e.TrangThai == TrangThaiKichBan.DangHinhThanh && e.NgayDanhGia == ngayDanhGia)
            .ToListAsync(ct);

        if (formingEntities.Count == 0)
        {
            logger.LogDebug("Pha 2 — không có mã FORMING nào hôm nay.");
            return new Pha2KetQua(0, 0, Array.Empty<KetQuaKichBan>());
        }

        // 2. Lấy realtime quotes cho các mã FORMING
        var symbols = formingEntities.Select(e => e.Symbol).Distinct().ToList();
        var (giaHienTai, volumeHienTai) = await LayRealtimeQuotesAsync(symbols, ct);

        if (giaHienTai.Count == 0)
        {
            logger.LogWarning("Pha 2 — không lấy được realtime quotes cho {Count} mã.", symbols.Count);
            return new Pha2KetQua(0, 0, Array.Empty<KetQuaKichBan>());
        }

        // 3. Chuyển entity → domain model rồi kiểm tra trigger
        var domainModels = formingEntities.Select(ChuyenSangDomain).ToList();
        var daKichHoat = await mayNhanKichBan.KiemTraTriggerTrongPhienAsync(
            domainModels, giaHienTai, volumeHienTai, ct);

        if (daKichHoat.Count == 0)
        {
            logger.LogDebug("Pha 2 — {Count} mã FORMING, chưa có trigger.", formingEntities.Count);
            return new Pha2KetQua(0, 0, Array.Empty<KetQuaKichBan>());
        }

        // 4. Xếp hạng (nếu nhiều hơn TopN trigger cùng lúc)
        var daXepHang = await xepHang.XepHangAsync(daKichHoat, ct);

        // 5. Cập nhật DB: chuyển state FORMING → TRIGGERED, lưu KeHoach + BangChup JSON
        await CapNhatDbAsync(daKichHoat, daXepHang, formingEntities, ct);

        // 6. Bắn Telegram alert cho các mã đã xếp hạng
        var soAlert = 0;
        if (telegramOptions.Value.Enabled)
        {
            foreach (var item in daXepHang)
            {
                ct.ThrowIfCancellationRequested();
                await BanAlertTelegramAsync(item, ct);
                soAlert++;
            }
        }

        // 7. Kiểm tra SELL cho các mã đang giữ vị thế (HOLDING)
        await KiemTraSellAsync(ngayDanhGia, ct);

        logger.LogInformation(
            "Pha 2 hoàn tất: {Trigger} trigger, {Alert} alert từ {Forming} mã FORMING.",
            daKichHoat.Count, soAlert, formingEntities.Count);

        return new Pha2KetQua(daKichHoat.Count, soAlert, daKichHoat);
    }

    /// <summary>Lấy giá + volume realtime từ KBS price board cho danh sách symbol.</summary>
    private async Task<(Dictionary<string, decimal> Gia, Dictionary<string, long> Volume)> LayRealtimeQuotesAsync(
        IReadOnlyList<string> symbols,
        CancellationToken ct)
    {
        var gia = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var volume = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < symbols.Count; i += BatchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = symbols.Skip(i).Take(BatchSize).ToList();
            var board = await kbs.FetchAsync(batch, ct);

            foreach (var row in board)
            {
                if (row.Close > 0)
                {
                    gia[row.Symbol] = row.Close;
                    volume[row.Symbol] = row.SessionVolume;
                }
            }
        }

        return (gia, volume);
    }

    /// <summary>Chuyển KetQuaKichBanEntity (DB) → KetQuaKichBan (domain model).</summary>
    private static KetQuaKichBan ChuyenSangDomain(KetQuaKichBanEntity entity)
    {
        var bangChung = new List<Domain.ValueObjects.BangChung>();
        if (!string.IsNullOrEmpty(entity.DanhSachBangChungJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<Domain.ValueObjects.BangChung>>(entity.DanhSachBangChungJson);
                if (parsed != null) bangChung = parsed;
            }
            catch { /* JSON cũ không parse được → bỏ qua */ }
        }

        KeHoachGiaoDich? keHoach = null;
        if (!string.IsNullOrEmpty(entity.KeHoachGiaoDichJson))
        {
            try { keHoach = JsonSerializer.Deserialize<Domain.ValueObjects.KeHoachGiaoDich>(entity.KeHoachGiaoDichJson); }
            catch { /* bỏ qua */ }
        }

        return new KetQuaKichBan
        {
            Symbol = entity.Symbol,
            LoaiKichBan = entity.LoaiKichBan,
            TrangThai = entity.TrangThai,
            DatBoiCanh = entity.DatBoiCanh,
            DatHinhThai = entity.DatHinhThai,
            DatCoKichHoat = entity.DatCoKichHoat,
            ThoiGianKichHoat = entity.ThoiGianKichHoat,
            KeHoach = keHoach,
            DanhSachBangChung = bangChung,
            DiemXepHang = entity.DiemXepHang
        };
    }

    /// <summary>Cập nhật DB: FORMING → TRIGGERED + lưu kế hoạch, snapshot, điểm xếp hạng.</summary>
    private async Task CapNhatDbAsync(
        IReadOnlyList<KetQuaKichBan> daKichHoat,
        IReadOnlyList<KetQuaXepHang> daXepHang,
        IReadOnlyList<KetQuaKichBanEntity> formingEntities,
        CancellationToken ct)
    {
        // Map điểm xếp hạng theo (Symbol, LoaiKichBan)
        var diemMap = daXepHang.ToDictionary(
            x => (x.KetQua.Symbol, x.KetQua.LoaiKichBan),
            x => x.DiemTong);

        foreach (var k in daKichHoat)
        {
            var entity = formingEntities.FirstOrDefault(
                e => e.Symbol == k.Symbol && e.LoaiKichBan == k.LoaiKichBan);
            if (entity == null) continue;

            entity.TrangThai = TrangThaiKichBan.DaKichHoat;
            entity.DatCoKichHoat = true;
            entity.MucHoanThien = k.MucHoanThien;
            entity.ThoiGianKichHoat = k.ThoiGianKichHoat;
            entity.KeHoachGiaoDichJson = k.KeHoach != null ? JsonSerializer.Serialize(k.KeHoach) : null;
            entity.BangChupChiBaoJson = k.BangChup != null ? JsonSerializer.Serialize(k.BangChup) : null;
            entity.DanhSachBangChungJson = JsonSerializer.Serialize(k.DanhSachBangChung);

            if (diemMap.TryGetValue((k.Symbol, k.LoaiKichBan), out var diem))
                entity.DiemXepHang = diem;

            entity.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Bắn Telegram alert cho một kịch bản đã trigger + xếp hạng.</summary>
    private async Task BanAlertTelegramAsync(KetQuaXepHang xepHangItem, CancellationToken ct)
    {
        var ketQua = xepHangItem.KetQua;
        var message = V2TelegramFormatter.FormatMua(ketQua, xepHangItem);
        await telegram.SendAsync(message, ct);
    }

    /// <summary>
    /// Kiểm tra SELL cho các mã đang ở trạng thái HOLDING (đã trigger BUY trước đó).
    /// Gọi DanhGiaBanAsync cho từng mã, nếu FORMING → check trigger bán.
    /// </summary>
    private async Task KiemTraSellAsync(DateTime ngayDanhGia, CancellationToken ct)
    {
        // Lấy các mã đang giữ vị thế (DaKichHoat BUY từ trước → coi như HOLDING)
        var holdingEntities = await db.KetQuaKichBan
            .Where(e => e.TrangThai == TrangThaiKichBan.DaKichHoat
                        && e.NgayDanhGia == ngayDanhGia
                        && e.LoaiKichBan != LoaiKichBan.KietSuc
                        && e.LoaiKichBan != LoaiKichBan.GayNen)
            .ToListAsync(ct);

        if (holdingEntities.Count == 0)
            return;

        var holdingSymbols = holdingEntities.Select(e => e.Symbol).Distinct().ToList();
        var (giaHienTai, volumeHienTai) = await LayRealtimeQuotesAsync(holdingSymbols, ct);

        foreach (var symbol in holdingSymbols)
        {
            ct.ThrowIfCancellationRequested();
            if (!giaHienTai.TryGetValue(symbol, out var gia))
                continue;

            var stock = await stockRepo.GetBySymbolAsync(symbol, ct);
            if (stock?.History == null || stock.History.Count == 0)
                continue;

            // Đánh giá kịch bản bán
            var sellResults = await mayNhanKichBan.DanhGiaBanAsync(symbol, stock.History, gia, ct);
            var sellForming = sellResults
                .Where(r => r.TrangThai == TrangThaiKichBan.DangHinhThanh)
                .ToList();

            if (sellForming.Count == 0)
                continue;

            // Kiểm tra trigger bán
            var sellTriggered = await mayNhanKichBan.KiemTraTriggerTrongPhienAsync(
                sellForming,
                new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { [symbol] = gia },
                new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { [symbol] = volumeHienTai.GetValueOrDefault(symbol, 0) },
                ct);

            foreach (var sell in sellTriggered)
            {
                var message = V2TelegramFormatter.FormatBan(sell);
                await telegram.SendAsync(message, ct);

                logger.LogInformation(
                    "Pha 2 SELL — {Loai} TRIGGERED cho {Symbol} tại {Gia:F2}.",
                    sell.LoaiKichBan, sell.Symbol, gia);
            }
        }
    }
}
