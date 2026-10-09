using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Constants;
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
    Pha2TrailingStopRuntime trailingStopRuntime,
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
        var (giaHienTai, volumeHienTai, _, _) = await LayRealtimeQuotesAsync(symbols, ct);

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

    /// <summary>Lấy giá + volume + high/low trong phiên từ KBS price board cho danh sách symbol.</summary>
    private async Task<(
        Dictionary<string, decimal> Gia,
        Dictionary<string, long> Volume,
        Dictionary<string, decimal> High,
        Dictionary<string, decimal> Low)> LayRealtimeQuotesAsync(
        IReadOnlyList<string> symbols,
        CancellationToken ct)
    {
        var gia = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var volume = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var high = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var low = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
    
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
                    high[row.Symbol] = row.High > 0 ? row.High : row.Close;
                    low[row.Symbol] = row.Low > 0 ? row.Low : row.Close;
                }
            }
        }
    
        return (gia, volume, high, low);
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
    /// Xét bán cho mọi vị thế còn giữ (chưa thoát hết): theo dõi MAE/MFE + đỉnh, dừng lỗ
    /// kế hoạch / dời về giá vào / đuổi theo ATR (có xác nhận chống nhiễu), dừng lỗ theo
    /// thời gian, và hết hạn theo dõi. Đánh giá Kiệt sức / Gãy nền cuối cùng.
    /// </summary>
    /// <param name="ngayDanhGia">Ngày đánh giá hiện tại (phiên giao dịch).</param>
    /// <param name="ct">Token hủy.</param>
    internal async Task KiemTraSellAsync(DateTime ngayDanhGia, CancellationToken ct)
    {
        // DO-NOT-CHANGE: không lọc theo NgayDanhGia; lọc theo ngày thì chỉ xét mã mua hôm nay
        // (chưa bán được vì T+2.5) và bỏ sót mọi vị thế cũ đang giữ.
        // Không lọc KetQuaDoLuong == null: Pha 3 đo ở T+3 rồi đổi TrangThai → ChotLoi/HuyLenh,
        // nhưng vị thế thực vẫn mở nên Pha 2 phải theo dõi tới khi thoát hết hoặc quá
        // SoPhienTheoDoiToiDa phiên (mục 4 spec C).
        var holdingEntities = await db.KetQuaKichBan
            .Where(e => e.TrangThai != TrangThaiKichBan.DangHinhThanh
                        && e.TrangThai != TrangThaiKichBan.DangTheoDoi
                        && e.LoaiKichBan != LoaiKichBan.KietSuc
                        && e.LoaiKichBan != LoaiKichBan.GayNen
                        && e.ThoiGianKichHoat != null
                        && e.ThoiGianThoatHet == null)
            .ToListAsync(ct);

        if (holdingEntities.Count == 0)
            return;

        var bySymbol = holdingEntities
            .GroupBy(e => e.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var symbols = bySymbol.Select(g => g.Key).ToList();
        var (giaHienTai, volumeHienTai, highHienTai, lowHienTai) = await LayRealtimeQuotesAsync(symbols, ct);
        
        var cfg = pha2Options.Value;
        var homNay = VietnamMarketCalendar.TodayVietnam();
        var nowVn = VietnamMarketCalendar.NowVietnam();
        var gioBatDauAtc = TimeSpan.Parse(cfg.GioBatDauAtc);
        var luotXacNhanHienHanh = nowVn.TimeOfDay >= gioBatDauAtc
            ? cfg.SoLuotXacNhanDungLoAtc
            : cfg.SoLuotXacNhanDungLo;

        foreach (var group in bySymbol)
        {
            var symbol = group.Key;
            ct.ThrowIfCancellationRequested();
            if (!giaHienTai.TryGetValue(symbol, out var gia))
                continue;

            var entities = group.ToList();

            // Vị thế đã thoát hết → bỏ qua hoàn toàn
            if (entities.Any(e => e.ThoiGianThoatHet != null))
                continue;

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
            var ngayKichHoat = DateOnly.FromDateTime(latestWithPlan.ThoiGianKichHoat!.Value);
            var soPhienDaQua = TradingSessionMath.TradingSessionsBetween(ngayKichHoat, homNay);
            
            // Hết hạn theo dõi → bán hết (rào thời gian triple-barrier).
            if (soPhienDaQua > cfg.SoPhienTheoDoiToiDa)
            {
                var hetHanMsg = V2TelegramFormatter.FormatHetHanTheoDoi(symbol, gia, giaVao, soPhienDaQua, cfg.SoPhienTheoDoiToiDa);
                await telegram.SendAsync(hetHanMsg, ct);
                foreach (var e in entities)
                {
                    e.ThoiGianThoatHet = DateTime.UtcNow;
                    e.GiaThoatHet = gia;
                    e.LyDoThoatHet = SuKienBan.LyDoHetHanTheoDoi;
                }
                await db.SaveChangesAsync(ct);
                trailingStopRuntime.ResetConfirmation(symbol);
                logger.LogInformation(
                    "Pha 2 SELL — HetHanTheoDoi {Symbol} sau {N} phiên.", symbol, soPhienDaQua);
                continue;
            }
            
            // === MAE / MFE / DinhTuLucMua ===
            // Phiên mua: bỏ session High/Low (đỉnh/đáy có thể xảy ra trước lúc mua).
            // Các phiên sau: dùng max/min kết hợp giá khớp để không lỡ đỉnh/đáy giữa 2 tick.
            var laPhienMua = homNay == ngayKichHoat;
            var maxCao = laPhienMua
                ? gia
                : Math.Max(gia, highHienTai.GetValueOrDefault(symbol, gia));
            var minThap = laPhienMua
                ? gia
                : Math.Min(gia, lowHienTai.GetValueOrDefault(symbol, gia));
            var pctMax = (maxCao - giaVao) / giaVao * 100m;
            var pctMin = (minThap - giaVao) / giaVao * 100m;
            var coThayDoiSoLieu = false;
            foreach (var e in entities)
            {
                var mfeMoi = Math.Max(e.Mfe ?? 0m, pctMax);
                var maeMoi = Math.Min(e.Mae ?? 0m, pctMin);
                var dinhMoi = Math.Max(e.DinhTuLucMua ?? 0m, maxCao);
                if (e.Mfe != mfeMoi) { e.Mfe = mfeMoi; coThayDoiSoLieu = true; }
                if (e.Mae != maeMoi) { e.Mae = maeMoi; coThayDoiSoLieu = true; }
                if (e.DinhTuLucMua != dinhMoi) { e.DinhTuLucMua = dinhMoi; coThayDoiSoLieu = true; }
            }
            if (coThayDoiSoLieu)
                await db.SaveChangesAsync(ct);
            
            var dinhTuLucMua = entities.First().DinhTuLucMua ?? maxCao;
            
            // === Dừng lỗ đuổi theo ATR (chỉ kịch bản theo đà) ===
            // ATR cache theo (mã, phiên), tính một lần rồi giữ.
            var loaiDinhKem = latestWithPlan.LoaiKichBan.ToString();
            var batDungLoDuoi = cfg.KichBanDungLoDuoi is not null
                && cfg.KichBanDungLoDuoi.Contains(loaiDinhKem, StringComparer.OrdinalIgnoreCase);
            var dungLoDuoiHienTai = entities.First().DungLoDuoi;
            if (batDungLoDuoi && dinhTuLucMua > 0)
            {
                var stockAtr = await stockRepo.GetBySymbolAsync(symbol, ct);
                if (stockAtr?.History != null && stockAtr.History.Count >= cfg.ChuKyAtr)
                {
                    var atr = trailingStopRuntime.GetOrComputeAtr(symbol, homNay, () =>
                        IndicatorMath.Atr(stockAtr.History, cfg.ChuKyAtr));
                    if (atr > 0)
                    {
                        var doRongAtr = cfg.HeSoAtrDungLoDuoi * atr;
                        if (doRongAtr / giaVao > 0.07m)
                            logger.LogWarning(
                                "Pha 2 SELL — {Symbol}: k*ATR = {P:P0} vượt biên độ ±7%, dừng lỗ đuổi theo có thể không chạm trong một phiên.",
                                symbol, doRongAtr / giaVao);
                        var moi = dinhTuLucMua - doRongAtr;
                        var giaTriMoi = Math.Max(dungLoDuoiHienTai ?? 0m, moi);
                        if (giaTriMoi > 0 && giaTriMoi != (dungLoDuoiHienTai ?? 0m))
                        {
                            foreach (var e in entities) e.DungLoDuoi = giaTriMoi;
                            await db.SaveChangesAsync(ct);
                            dungLoDuoiHienTai = giaTriMoi;
                        }
                    }
                }
            }
            
            var daChotLoi1 = entities.Any(e => e.ThoiGianBanNua != null);
            
            // === Luật mức giá (dừng lỗ + TP1 + TP2) + chống nhiễu ===
            string? suKien = null;
            string? lyDoThoat = null;
            string? themLineDungLo = null;
            
            if (keHoach.GiaDungLo > 0 && keHoach.GiaChotLoi1 > 0)
            {
                // Dừng lỗ hiệu lực = max của 3 ứng viên: kế hoạch / dời về giá vào / đuổi theo ATR.
                var slKeHoach = keHoach.GiaDungLo;
                var slDoiVeGiaVao = entities.Any(e => e.DaDoiDungLo) ? giaVao : 0m;
                var slDuoiTheo = dungLoDuoiHienTai ?? 0m;
                var dungLoHieuLuc = Math.Max(slKeHoach, Math.Max(slDoiVeGiaVao, slDuoiTheo));
            
                if (gia <= dungLoHieuLuc)
                {
                    // Xác nhận N lượt liên tiếp (chỉ cho dừng lỗ — không áp cho chốt lời).
                    var luot = trailingStopRuntime.IncrementConfirmation(symbol, homNay);
                    if (luot >= luotXacNhanHienHanh)
                    {
                        trailingStopRuntime.ResetConfirmation(symbol);
                        if (slDuoiTheo > 0 && slDuoiTheo >= slKeHoach && slDuoiTheo >= slDoiVeGiaVao)
                        {
                            suKien = SuKienBan.ChamDungLoDuoi;
                            lyDoThoat = SuKienBan.LyDoDungLoDuoi;
                            themLineDungLo = $"Mức áp dụng: dừng lỗ đuổi theo <code>{slDuoiTheo:0.##}</code>";
                        }
                        else if (slDoiVeGiaVao > 0 && slDoiVeGiaVao >= slKeHoach)
                        {
                            suKien = SuKienBan.ChamDungLo;
                            lyDoThoat = SuKienBan.LyDoDungLo;
                            themLineDungLo = $"Mức áp dụng: dừng lỗ dời về giá vào <code>{giaVao:0.##}</code>";
                        }
                        else
                        {
                            suKien = SuKienBan.ChamDungLo;
                            lyDoThoat = SuKienBan.LyDoDungLo;
                            themLineDungLo = $"Mức áp dụng: dừng lỗ kế hoạch <code>{slKeHoach:0.##}</code>";
                        }
                    }
                }
                else
                {
                    // Giá hồi trên dừng lỗ → mất chuỗi liên tiếp.
                    trailingStopRuntime.ResetConfirmation(symbol);
            
                    if (keHoach.GiaChotLoi2 > 0 && gia >= keHoach.GiaChotLoi2 && daChotLoi1)
                    {
                        suKien = SuKienBan.ChamChotLoi2;
                        lyDoThoat = SuKienBan.LyDoChotLoi2;
                    }
                    else if (gia >= keHoach.GiaChotLoi1 && !daChotLoi1)
                    {
                        suKien = SuKienBan.ChamChotLoi1;
                    }
                }
            }
            
            // === Dừng lỗ theo thời gian ===
            // Chưa bán nửa (TP1/Kiệt sức) + đủ số phiên qui định + MFE dưới ngưỡng.
            if (suKien is null
                && !daChotLoi1
                && soPhienDaQua >= cfg.SoPhienDungTheoThoiGian
                && (entities.First().Mfe ?? 0m) < cfg.NguongMfeToiThieuPhanTram)
            {
                suKien = SuKienBan.HetThoiGian;
                lyDoThoat = SuKienBan.LyDoHetThoiGian;
            }
            
            if (suKien is not null)
            {
                if (soPhienDaQua < cfg.MinTradingSessionsToSell)
                {
                    // BUSINESS-RULE: chưa đủ T+2.5 → gửi cảnh báo một lần, KHÔNG đổi trạng thái vị thế.
                    var canhBaoStr = entities.First().CanhBaoDaGui ?? "";
                    if (!CanhBaoChuaSuKien(canhBaoStr, suKien))
                    {
                        var conLai = cfg.MinTradingSessionsToSell - soPhienDaQua;
                        var tenHienThi = suKien switch
                        {
                            SuKienBan.ChamDungLo     => "CHẠM DỪNG LỖ",
                            SuKienBan.ChamDungLoDuoi => "CHẠM DỪNG LỖ ĐUỔI THEO",
                            SuKienBan.ChamChotLoi1   => "CHẠM CHỐT LỜI 1",
                            SuKienBan.ChamChotLoi2   => "CHẠM CHỐT LỜI 2",
                            SuKienBan.HetThoiGian    => "HẾT THỜI GIAN",
                            _ => suKien,
                        };
                        var wMsg = V2TelegramFormatter.FormatCanhBaoChuaBanDuoc(symbol, tenHienThi, conLai);
                        await telegram.SendAsync(wMsg, ct);
                        foreach (var e in entities)
                            e.CanhBaoDaGui = ThemCanhBao(e.CanhBaoDaGui, suKien);
                        await db.SaveChangesAsync(ct);
                        logger.LogInformation(
                            "Pha 2 SELL WARNING — {SuKien} {Symbol}: con {N} phien.", suKien, symbol, conLai);
                    }
                }
                else
                {
                    string sellMsg;
                    if (suKien == SuKienBan.ChamDungLo)
                        sellMsg = V2TelegramFormatter.FormatChamMucGia(
                            symbol, "Chạm dừng lỗ", "BÁN HẾT", gia, giaVao, themLineDungLo);
                    else if (suKien == SuKienBan.ChamDungLoDuoi)
                        sellMsg = V2TelegramFormatter.FormatChamMucGia(
                            symbol, "Chạm dừng lỗ đuổi theo", "BÁN HẾT", gia, giaVao, themLineDungLo);
                    else if (suKien == SuKienBan.ChamChotLoi1)
                        sellMsg = V2TelegramFormatter.FormatChamMucGia(
                            symbol, "Chạm chốt lời 1", "BÁN 50%", gia, giaVao,
                            $"Dừng lỗ dời về <code>{giaVao:0.##}</code>");
                    else if (suKien == SuKienBan.ChamChotLoi2)
                        sellMsg = V2TelegramFormatter.FormatChamMucGia(
                            symbol, "Chạm chốt lời 2", "BÁN HẾT", gia, giaVao, null);
                    else
                        sellMsg = V2TelegramFormatter.FormatHetThoiGian(
                            symbol, gia, giaVao, soPhienDaQua,
                            entities.First().Mfe ?? 0m, cfg.NguongMfeToiThieuPhanTram);
            
                    await telegram.SendAsync(sellMsg, ct);
            
                    // Ghi trạng thái (TelegramNotifier nuốt lỗi — gửi hỏng vẫn ghi)
                    if (suKien == SuKienBan.ChamChotLoi1)
                    {
                        foreach (var e in entities)
                        {
                            e.ThoiGianBanNua = DateTime.UtcNow;
                            e.GiaBanNua = gia;
                            e.DaDoiDungLo = true;
                        }
                    }
                    else
                    {
                        // DungLo / DungLoDuoi / ChotLoi2 / HetThoiGian đều là bán hết.
                        foreach (var e in entities)
                        {
                            e.ThoiGianThoatHet = DateTime.UtcNow;
                            e.GiaThoatHet = gia;
                            e.LyDoThoatHet = lyDoThoat ?? SuKienBan.LyDoDungLo;
                        }
                    }
                    await db.SaveChangesAsync(ct);
            
                    logger.LogInformation(
                        "Pha 2 SELL — {SuKien} {Symbol} tai {Gia:F2} (giaVao={Entry:F2}).",
                        suKien, symbol, gia, giaVao);
            
                    // Bán nửa (TP1) → vẫn còn vị thế, cho phép Kiệt/Gãy. Các sự kiện khác → thoát hết.
                    if (suKien != SuKienBan.ChamChotLoi1)
                        continue;
                }
            }

            // === Bước 2: Kiệt sức / Gãy nền — dùng chung trạng thái vị thế ===
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

                // Chặn lặp:
                // KietSuc = bán nửa — bỏ qua nếu đã bán nửa hoặc đã thoát hết
                if (loaiBan == LoaiKichBan.KietSuc
                    && (entities.Any(e => e.ThoiGianBanNua != null)
                        || entities.Any(e => e.ThoiGianThoatHet != null)))
                    continue;

                // GayNen = thoát hết — bỏ qua nếu đã thoát; cho phép nâng cấp từ KietSuc
                if (loaiBan == LoaiKichBan.GayNen && entities.Any(e => e.ThoiGianThoatHet != null))
                    continue;

                var canBaoKey = loaiBan == LoaiKichBan.KietSuc ? SuKienBan.KietSuc : SuKienBan.GayNen;

                // BUSINESS-RULE: chưa đủ phiên T+2.5 thì chưa bán được, chỉ gửi cảnh báo một lần.
                if (soPhienDaQua < cfg.MinTradingSessionsToSell)
                {
                    var canhBaoStr = entities.First().CanhBaoDaGui ?? "";
                    if (CanhBaoChuaSuKien(canhBaoStr, canBaoKey))
                        continue;

                    var conLai = cfg.MinTradingSessionsToSell - soPhienDaQua;
                    var warningMsg = V2TelegramFormatter.FormatCanhBaoChuaBanDuoc(sell, conLai);
                    await telegram.SendAsync(warningMsg, ct);

                    foreach (var e in entities)
                        e.CanhBaoDaGui = ThemCanhBao(e.CanhBaoDaGui, canBaoKey);
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
                if (loaiBan == LoaiKichBan.KietSuc)
                {
                    // Kiệt sức = bán nửa, KHÔNG dời dừng lỗ
                    foreach (var e in entities)
                    {
                        e.ThoiGianBanNua = DateTime.UtcNow;
                        e.GiaBanNua = gia;
                    }
                }
                else // GayNen
                {
                    foreach (var e in entities)
                    {
                        e.ThoiGianThoatHet = DateTime.UtcNow;
                        e.GiaThoatHet = gia;
                        e.LyDoThoatHet = SuKienBan.LyDoGayNen;
                    }
                }
                await db.SaveChangesAsync(ct);

                logger.LogInformation(
                    "Pha 2 SELL — {Loai} TRIGGERED cho {Symbol} tai {Gia:F2} (giaVao={Entry:F2}).",
                    loaiBan, symbol, gia, giaVao);
            }
        }
    }

    /// <summary>Kiểm tra một sự kiện đã có trong danh sách cảnh báo (CSV) chưa.</summary>
    /// <param name="canhBaoStr">Giá trị hiện tại của cột CanhBaoDaGui.</param>
    /// <param name="suKien">Tên sự kiện cần kiểm tra.</param>
    /// <returns>true nếu đã gửi cảnh báo cho sự kiện này.</returns>
    private static bool CanhBaoChuaSuKien(string? canhBaoStr, string suKien) =>
        !string.IsNullOrWhiteSpace(canhBaoStr)
        && canhBaoStr
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(suKien, StringComparer.OrdinalIgnoreCase);

    /// <summary>Thêm tên sự kiện vào danh sách cảnh báo CSV (không trùng lặp).</summary>
    /// <param name="hienTai">Giá trị hiện tại của cột CanhBaoDaGui.</param>
    /// <param name="suKien">Tên sự kiện cần thêm.</param>
    /// <returns>Chuỗi CSV mới.</returns>
    private static string ThemCanhBao(string? hienTai, string suKien)
    {
        if (string.IsNullOrWhiteSpace(hienTai))
            return suKien;
        var parts = hienTai
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (!parts.Contains(suKien, StringComparer.OrdinalIgnoreCase))
            parts.Add(suKien);
        return string.Join(",", parts);
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
