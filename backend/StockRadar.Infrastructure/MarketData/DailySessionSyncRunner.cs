using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Job 2: append nến phiên T sau đóng cửa (spec 018 Q2/Q4) — lần chính lấy toàn universe Job 1
/// rồi lọc lại danh sách mã; lần thử lại 15:15/15:30 chỉ lấy mã còn thiếu nến hôm nay.
/// </summary>
internal sealed class DailySessionSyncRunner(
    IJobStockRepository stocks,
    KbsPriceBoardClient kbs,
    KbsIndexClient indexClient,
    IMarketSyncService sync,
    IUniverseRescreenService universeRescreen,
    IOptions<MarketJobsOptions> options,
    Job2TrangThaiNgay trangThai,
    TimeProvider clock,
    ILogger<DailySessionSyncRunner> logger) : IDailySessionSyncService
{
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    public async Task<DailySessionSyncResultDto> RunAsync(
        CancellationToken cancellationToken = default,
        CheDoJob2 cheDo = CheDoJob2.LanChinh)
    {
        var stopwatch = Stopwatch.StartNew();
        var cfg = options.Value.DailySession;
        var sessionDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(clock.GetUtcNow().UtcDateTime, VietnamTimeZone));

        if (!TradingSessionMath.IsTradingDay(sessionDate))
        {
            logger.LogInformation("Job 2 — bỏ qua {Date} (không phải ngày giao dịch).", sessionDate);
            return new DailySessionSyncResultDto(0, false, sessionDate, DateTime.UtcNow);
        }

        if (cheDo == CheDoJob2.ThuLai && !trangThai.CoTrangThaiCuaNgay(sessionDate))
        {
            // Server khởi động lại sau lần chính nên chưa có trạng thái: chạy đầy đủ như lần chính nhưng không lọc lại danh sách mã.
            cheDo = CheDoJob2.ThuLaiDayDu;
        }

        if (cheDo == CheDoJob2.ThuLai && trangThai.MaConThieu.Count == 0)
        {
            logger.LogInformation("Job 2 thử lại: đủ nến, bỏ qua.");
            return new DailySessionSyncResultDto(0, false, sessionDate, DateTime.UtcNow);
        }

        List<string> symbols;
        if (cheDo == CheDoJob2.ThuLai)
        {
            symbols = trangThai.MaConThieu.Order(StringComparer.OrdinalIgnoreCase).ToList();
            logger.LogInformation("Job 2 — thử lại lần {Lan}: chỉ lấy {Count} mã còn thiếu nến.", trangThai.LanThu + 1, symbols.Count);
        }
        else
        {
            symbols = await LayToanBoMaAsync(sessionDate, cfg, cancellationToken);
            if (symbols.Count == 0)
                return new DailySessionSyncResultDto(0, false, sessionDate, DateTime.UtcNow);
        }

        var batchSize = Math.Max(10, cfg.BatchSize);
        var stocksUpdated = 0;
        var maConThieu = new List<string>();

        for (var i = 0; i < symbols.Count; i += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = symbols.Skip(i).Take(batchSize).ToList();
            var board = await kbs.FetchAsync(batch, cancellationToken);

            // "Thiếu" = mã có trong lô gửi KBS nhưng không nhận được dòng nào (KBS lỗi trả rỗng thì cả lô tính thiếu).
            var returned = board.Select(r => r.Symbol).ToHashSet(StringComparer.OrdinalIgnoreCase);
            maConThieu.AddRange(batch.Where(s => !returned.Contains(s)));

            var quotes = board
                .Select(r => new StockQuoteSyncDto(
                    r.Symbol, null, r.Open, r.High, r.Low, r.Close, r.SessionVolume, r.ChangePercent, null))
                .ToList();

            if (quotes.Count == 0)
                continue;

            var result = await sync.ApplyAsync(new MarketSyncRequest(null, quotes, SauDongCua: true), cancellationToken);
            stocksUpdated += result.StocksUpdated;

            logger.LogInformation(
                "Job 2 [{Done}/{Total}] batch {Batch} mã — tổng {Updated} đã ghi",
                Math.Min(i + batchSize, symbols.Count),
                symbols.Count,
                quotes.Count,
                stocksUpdated);
        }

        MarketIndexSyncDto? index = null;
        var vnIndex = await indexClient.FetchVnIndexAsync(cancellationToken);
        if (vnIndex is not null)
        {
            index = new MarketIndexSyncDto("VNINDEX", vnIndex.Price, vnIndex.ChangePercent);
            await sync.ApplyAsync(new MarketSyncRequest(index, [], SauDongCua: true), cancellationToken);
        }

        if (cheDo == CheDoJob2.ThuLai)
            trangThai.GhiSauLanThuLai(maConThieu);
        else
            trangThai.GhiSauLanDayDu(sessionDate, maConThieu);

        if (maConThieu.Count > 0 && trangThai.LanThu >= 3)
            logger.LogWarning(
                "Job 2: sau 15:30 vẫn còn {Count} mã thiếu nến hôm nay: {DanhSach}",
                maConThieu.Count,
                string.Join(", ", maConThieu.Distinct(StringComparer.OrdinalIgnoreCase).Take(20)));

        // FR-003: chỉ lần chính lọc lại danh sách mã — một lần mỗi ngày giao dịch.
        var universeDeactivated = 0;
        if (cheDo == CheDoJob2.LanChinh)
        {
            try
            {
                var rescreen = await universeRescreen.RunAsync(cancellationToken);
                universeDeactivated = rescreen.Deactivated;
                if (rescreen.Deactivated > 0 || rescreen.Reactivated > 0)
                    logger.LogInformation(
                        "Job 2 rescreen: loại {D}, khôi phục {R} mã.",
                        rescreen.Deactivated, rescreen.Reactivated);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Job 2: universe rescreen thất bại — bỏ qua, universe giữ nguyên.");
            }
        }

        logger.LogInformation(
            "Job 2 xong: {Stocks}/{Total} mã — chế độ {CheDo}, {ElapsedMs} ms, còn {Thieu} mã thiếu nến.",
            stocksUpdated,
            symbols.Count,
            cheDo,
            stopwatch.ElapsedMilliseconds,
            maConThieu.Count);

        return new DailySessionSyncResultDto(
            stocksUpdated,
            index is not null,
            sessionDate,
            DateTime.UtcNow,
            UniverseDeactivated: universeDeactivated);
    }

    /// <summary>Danh sách mã của lần chạy đầy đủ: active từ Job 1, kèm warm inactive nếu bật.</summary>
    private async Task<List<string>> LayToanBoMaAsync(
        DateOnly sessionDate,
        DailySessionJobOptions cfg,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Job 2 — append phiên {Date} cho universe Job 1.", sessionDate);

        var active = (await stocks.GetActiveSymbolsAsync(cancellationToken))
            .Where(s => !s.Equals("VNINDEX", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (active.Count == 0)
        {
            logger.LogWarning("Job 2: universe trống — chạy Job 1 trước.");
            return [];
        }

        // Warm mã inactive (không hạn chế GD): append nến phiên T để giữ lịch sử tươi, nhờ đó
        // rescreen cuối job tự khôi phục mã đã đạt thanh khoản trở lại — phá vòng chết
        // "inactive → không có giá mới → history đóng băng → không bao giờ khôi phục".
        var symbols = active;
        if (cfg.WarmInactiveUniverse)
        {
            var inactive = (await stocks.GetInactiveSymbolsAsync(cancellationToken))
                .Where(s => !s.Equals("VNINDEX", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (inactive.Count > 0)
            {
                symbols = active.Concat(inactive)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                logger.LogInformation(
                    "Job 2: warm {Inactive} mã inactive (tổng {Total} mã) để rescreen tự khôi phục.",
                    inactive.Count,
                    symbols.Count);
            }
        }

        return symbols;
    }
}
