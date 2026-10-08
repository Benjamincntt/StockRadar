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
    INguonLichChotQuyen lichChotQuyen,
    IOptions<FireAntOptions> fireAntOptions,
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

        // 6. Bắn Telegram alert cho các mã đã xếp hạng — NGĂN chia chác: mã đang trong
        // khoảng [ngày chốt quyền → ngày thực hiện quyền] không bắn noti mua (vẫn lưu
        // TRIGGERED vào DB như thường lệ, chỉ im lặng Telegram).
        var soAlert = 0;
        if (telegramOptions.Value.Enabled)
        {
            var chanChiaChac = await LayMapChiaChacAsync(todayVn, ct);
            foreach (var item in daXepHang)
            {
                ct.ThrowIfCancellationRequested();
                if (chanChiaChac.TryGetValue(item.KetQua.Symbol, out var thongTinChot))
                {
                    logger.LogInformation(
                        "Pha 2 — bỏ noti MUA {Symbol}: {Nhan}", item.KetQua.Symbol, thongTinChot.Nhan());
                    continue;
                }
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
    /// Danh sách mã đang bị cổng chia chác chặn (FireAnt, cache 6h tại client).
    /// Fail-open: tắt mã nguồn lỗi → map rỗng → không chặn noti nào.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, ThongTinChotQuyen>> LayMapChiaChacAsync(
        DateOnly homNay,
        CancellationToken ct)
    {
        var cfg = fireAntOptions.Value;
        if (!cfg.Enabled || cfg.LookaheadDays <= 0)
            return new Dictionary<string, ThongTinChotQuyen>();

        try
        {
            return await lichChotQuyen.LayMaSapChotQuyenAsync(homNay, cfg.LookaheadDays, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Pha 2 — không lấy được lịch chốt quyền, bỏ qua cổng chia chác.");
            return new Dictionary<string, ThongTinChotQuyen>();
        }
    }

    /// <summary>
    /// Xét bán cho mọi vị thế đang giữ chưa đo kết quả: gom theo mã,
    /// đánh giá kịch bản Kiệt sức / Gãy nền, chặn lặp, cảnh báo khi chưa đủ phiên T+2.5.
    /// </summary>
    /// <param name="ngayDanhGia">Ngày đánh giá hiện tại (phiên giao dịch).</param>
    /// <param name="ct">Token hủy.</param>
    internal async Task KiemTraSellAsync(DateTime ngayDanhGia, CancellationToken ct)
    {
        // DO-NOT-CHANGE: không lọc theo NgayDanhGia; lọc theo ngày thì chỉ xét mã mua hôm nay
        // (chưa bán được vì T+2.5) và bỏ sót mọi vị thế cũ đang giữ.
        var holdingEntities = await db.KetQuaKichBan
            .Where(e => e.TrangThai == TrangThaiKichBan.DaKichHoat
                        && e.LoaiKichBan != LoaiKichBan.KietSuc
                        && e.LoaiKichBan != LoaiKichBan.GayNen
                        && e.ThoiGianKichHoat != null
                        && e.KetQuaDoLuong == null)
            .ToListAsync(ct);

        if (holdingEntities.Count == 0)
            return;

        var bySymbol = holdingEntities
            .GroupBy(e => e.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var symbols = bySymbol.Select(g => g.Key).ToList();
        var (giaHienTai, volumeHienTai) = await LayRealtimeQuotesAsync(symbols, ct);

        var cfg = pha2Options.Value;
        var homNay = VietnamMarketCalendar.TodayVietnam();

        foreach (var group in bySymbol)
        {
            var symbol = group.Key;
            ct.ThrowIfCancellationRequested();
            if (!giaHienTai.TryGetValue(symbol, out var gia))
                continue;

            var entities = group.ToList();
            var latestWithPlan = entities
                .OrderByDescending(e => e.ThoiGianKichHoat)
                .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.KeHoachGiaoDichJson));

            if (latestWithPlan is null)
                continue;

            var keHoach = GiaiMaKeHoach(latestWithPlan.KeHoachGiaoDichJson);
            if (keHoach is null || keHoach.GiaVaoLenhMin <= 0)
            {
                logger.LogWarning(
                    "Pha 2 SELL — bo qua {Symbol}, khong giai ma duoc GiaVaoLenhMin.", symbol);
                continue;
            }

            var giaVao = keHoach.GiaVaoLenhMin;

            var stock = await stockRepo.GetBySymbolAsync(symbol, ct);
            if (stock?.History == null || stock.History.Count == 0)
                continue;

            // DO-NOT-CHANGE: phải truyền giá vào, truyền giá hiện tại thì lãi luôn ≈ 0%
            // và Kiệt sức không bao giờ kích hoạt (đòi MinGainFromEntry ≥ 10%).
            var sellResults = await mayNhanKichBan.DanhGiaBanAsync(symbol, stock.History, giaVao, ct);
            var sellForming = sellResults
                .Where(r => r.TrangThai == TrangThaiKichBan.DangHinhThanh)
                .ToList();

            if (sellForming.Count == 0)
                continue;

            var sellTriggered = await mayNhanKichBan.KiemTraTriggerTrongPhienAsync(
                sellForming,
                new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { [symbol] = gia },
                new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { [symbol] = volumeHienTai.GetValueOrDefault(symbol, 0) },
                ct);

            foreach (var sell in sellTriggered)
            {
                var loaiBan = sell.LoaiKichBan;

                var anyMarked = entities.Any(e => e.ThoiGianBaoBan != null);
                if (anyMarked)
                {
                    var existingType = entities
                        .Where(e => e.ThoiGianBaoBan != null)
                        .Select(e => e.LoaiBaoBan)
                        .FirstOrDefault();

                    // Cho phép nâng cấp: đã báo Kiệt sức (bán 50%) mà giờ Gãy nền (bán 100%) thì gửi thêm.
                    var isUpgrade = existingType == LoaiKichBan.KietSuc
                                    && loaiBan == LoaiKichBan.GayNen;
                    var alreadyGayNen = entities.Any(e =>
                        e.ThoiGianBaoBan != null && e.LoaiBaoBan == LoaiKichBan.GayNen);

                    if (!isUpgrade || alreadyGayNen)
                        continue;
                }

                // BUSINESS-RULE: chưa đủ phiên T+2.5 thì chưa bán được, chỉ gửi cảnh báo một lần.
                var ngayKichHoat = DateOnly.FromDateTime(latestWithPlan.ThoiGianKichHoat!.Value);
                var soPhienDaQua = TradingSessionMath.TradingSessionsBetween(ngayKichHoat, homNay);

                if (soPhienDaQua < cfg.MinTradingSessionsToSell)
                {
                    var alreadyWarned = entities.Any(e => e.ThoiGianCanhBaoBan != null);
                    if (alreadyWarned)
                        continue;

                    var conLai = cfg.MinTradingSessionsToSell - soPhienDaQua;
                    var warningMsg = V2TelegramFormatter.FormatCanhBaoChuaBanDuoc(sell, conLai);
                    await telegram.SendAsync(warningMsg, ct);

                    foreach (var e in entities)
                        e.ThoiGianCanhBaoBan = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);

                    logger.LogInformation(
                        "Pha 2 SELL WARNING — {Loai} {Symbol}: con {N} phien.",
                        loaiBan, symbol, conLai);
                    continue;
                }

                var message = V2TelegramFormatter.FormatBan(sell);
                await telegram.SendAsync(message, ct);

                // DO-NOT-CHANGE: không đổi TrangThai ở đây; đổi thì Pha 3 không tìm thấy
                // bản ghi DaKichHoat để đo kết quả.
                foreach (var e in entities)
                {
                    e.ThoiGianBaoBan = DateTime.UtcNow;
                    e.LoaiBaoBan = loaiBan;
                }
                await db.SaveChangesAsync(ct);

                logger.LogInformation(
                    "Pha 2 SELL — {Loai} TRIGGERED cho {Symbol} tai {Gia:F2} (giaVao={Entry:F2}).",
                    loaiBan, symbol, gia, giaVao);
            }
        }
    }

    /// <summary>Giải mã JSON kế hoạch giao dịch, trả null nếu rỗng hoặc hỏng.</summary>
    /// <param name="keHoachJson">Chuỗi JSON kế hoạch từ cột KeHoachGiaoDichJson.</param>
    /// <returns>Kế hoạch giao dịch đã parse, hoặc null.</returns>
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
