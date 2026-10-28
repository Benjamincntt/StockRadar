using Microsoft.Extensions.Logging;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Nhịp V3 TRONG PHIÊN (mỗi 1 phút, 09:00–14:45, Phụ lục D) — nhịp NHẸ NHẤT: KHÔNG nạp lịch sử,
/// KHÔNG gọi LayCacMaAsync, chỉ lấy giá của các mã đang giữ và so với những gì nhịp 16:00 đã lưu
/// (NgưỡngChamMa, GiaThamChieu, Exchange). Hai việc:
/// (1) khớp các vị thế CHỜ MUA (KB1/KB2 ATO) tại giá mở cửa — hủy nếu rơi giá trần (FR-014);
/// (2) xét BÁN realtime cho mọi vị thế ĐANG GIỮ trên giá khớp (V3-B1): thoát theo ngưỡng
///     max(chạm MAn, giá vào +0,4%) hoặc N3 ATO theo lịch. HetHanATC nhường cho nhịp ATC.
/// </summary>
internal sealed class V3TrongPhienRunner(
    V3Engine engine,
    ITelegramNotifier telegram,
    ILogger<V3TrongPhienRunner> logger) : IV3TrongPhienService
{
    public async Task<V3ChayKetQua> ChayAsync(CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            return await ChayBenTrongAsync(ct);
        }
        finally
        {
            sw.Stop();
            logger.LogInformation("V3 trong phiên: nhịp chạy hết {Ms} ms.", sw.ElapsedMilliseconds);
        }
    }

    private async Task<V3ChayKetQua> ChayBenTrongAsync(CancellationToken ct)
    {
        var homNay = VietnamMarketCalendar.TodayVietnam();
        var gioXet = TimeOnly.FromDateTime(VietnamMarketCalendar.NowVietnam());
        if (!VietnamMarketCalendar.IsTradingDay(homNay) || !VietnamMarketCalendar.IsMarketOpen())
            return new V3ChayKetQua(0, 0, 0);

        var cfg = engine.Config;
        var viTheMo = await engine.LayViTheDangMoAsync(ct);
        if (viTheMo.Count == 0)
            return new V3ChayKetQua(0, 0, 0);

        var symbols = viTheMo.Select(v => v.Symbol).Distinct().ToList();
        var (gia, _, high, low) = await engine.LayRealtimeQuotesAsync(symbols, ct);
        if (gia.Count == 0)
            return new V3ChayKetQua(0, 0, 0);

        // (1) Khớp CHỜ MUA (ATO mở cửa) → ĐANG GIỮ, hoặc HỦY nếu giá trần.
        foreach (var vt in viTheMo.Where(v => v.TrangThai == TrangThaiViTheV3.ChoMua))
        {
            if (!gia.TryGetValue(vt.Symbol, out var gk) || gk <= 0m)
                continue; // chưa có giá mở cửa — chờ nhịp sau
            var refP = await LayGiaThamChieuAsync(vt, ct);
            if (refP > 0m && V3PhienHoTro.LaGiaTran(gk, refP, vt.Exchange))
            {
                await engine.HuyLenhTranAsync(vt, ct);
                logger.LogInformation("V3 HỦY mua (giá trần) — {Symbol} {Loai} @ {Gia}.", vt.Symbol, vt.Loai, gk);
                continue;
            }
            var kl = V3Engine.KhoiLuongTheoLo(vt.GiaTriLenh ?? 0m, gk);
            if (kl <= 0m)
                continue;
            await engine.KhopChoMuaAsync(vt, homNay, gk, kl, ct);
            logger.LogInformation("V3 KHỚP mua — {Symbol} {Loai} @ {Gia} x {KL}.", vt.Symbol, vt.Loai, gk, kl);
        }

        // (2) Xét BÁN realtime cho ĐANG GIỮ — so giá khớp với ngưỡng đã lưu, không đụng lịch sử.
        int soLenhThoat = 0;
        foreach (var vt in viTheMo.Where(v => v.TrangThai == TrangThaiViTheV3.DangGiu))
        {
            if (!gia.TryGetValue(vt.Symbol, out var gk) || gk <= 0m)
                continue;

            var refP = vt.GiaThamChieu ?? 0m;
            var sanTrang = refP > 0m && V3PhienHoTro.LaSanTrang(
                gk, high.GetValueOrDefault(vt.Symbol), low.GetValueOrDefault(vt.Symbol), refP, vt.Exchange);
            var soPhien = V3PhienHoTro.SoPhienDaGi(vt.NgayTinHieu, homNay);
            var daQua = V3PhienHoTro.DaQuaTplus25(vt.NgayVao, homNay, gioXet, cfg);
            var denLichAtoN3 = vt.Loai == LoaiKichBanV3.N3 && V3LichPhien.LaPhienAtoN3(homNay);
            // Nhịp 16:00 chưa kịp ghi ngưỡng (vị thế mở trong ngày) → dùng tối thiểu phần phí.
            var nguong = vt.NguongChamMa ?? (vt.GiaVao ?? 0m) * cfg.BanPhanTramGiaVao;

            var kq = V3LuatBanThucTe.XetTheoNguongDaLuu(
                vt.Loai, nguong, gk, soPhien,
                V3LuatBanThucTe.GiuToiDaTheoViThe(cfg, vt.Loai, vt.LaHoangLoanSau),
                denLichAtoN3, daQua, sanTrang, vt.LaHoangLoanSau);

            // Trong phiên: chỉ xử lý thoát theo giá (ChamMA / Chốt hoảng loạn) và N3 ATO theo lịch; để dành HetHanATC cho nhịp ATC.
            if (kq.Thoat && kq.LyDo != V3LuatBanThucTe.LyDoThoat.HetHanATC)
            {
                await engine.KhopBanAsync(vt, homNay, gk, kq.LyDo.ToString(), ct);
                var msg = V3TelegramFormatter.FormatBan(
                    vt.Loai, vt.Symbol, gk, vt.GiaVao ?? 0m, soPhien,
                    V3LuatBanThucTe.GiuToiDaTheoViThe(cfg, vt.Loai, vt.LaHoangLoanSau),
                    LyDoHienThi(kq.LyDo));
                await telegram.SendAsync(msg, ct, TelegramNotifier.HtmlParseMode);
                soLenhThoat++;
                logger.LogInformation("V3 BÁN realtime — {Symbol} {Loai} @ {Gia} ({LyDo}).",
                    vt.Symbol, vt.Loai, gk, kq.LyDo);
            }
        }

        await engine.LuuAsync(ct);
        return new V3ChayKetQua(0, 0, soLenhThoat);
    }

    /// <summary>
    /// Giá tham chiếu phiên hiện tại của vị thế: cột đã lưu lúc 16:00; hàng cũ chưa có → lấy
    /// giá tín hiệu đã mirror (chính là đóng cửa phiên T, tức giá tham chiếu của phiên ATO T+1).
    /// </summary>
    private async Task<decimal> LayGiaThamChieuAsync(ViTheV3Entity vt, CancellationToken ct)
    {
        if (vt.GiaThamChieu is > 0m)
            return vt.GiaThamChieu.Value;
        // Hàng cũ chưa có cột giá tham chiếu: giá tín hiệu = đóng cửa phiên T = giá tham chiếu phiên ATO T+1.
        var th = await engine.TimTinHieuAsync(vt.TinHieuV3Id, ct);
        return th?.GiaTinHieu ?? 0m;
    }

    private static string LyDoHienThi(V3LuatBanThucTe.LyDoThoat lyDo) => lyDo switch
    {
        V3LuatBanThucTe.LyDoThoat.ChamMA => "Chạm ngưỡng MA + phí",
        V3LuatBanThucTe.LyDoThoat.ChotLoiHoangLoan => "Chốt +15% hoảng loạn sâu",
        V3LuatBanThucTe.LyDoThoat.AtoTheoLich => "ATO theo lịch đầu tháng sau",
        V3LuatBanThucTe.LyDoThoat.HetHanATC => "Hết hạn giữ (ATC)",
        _ => "Thoát",
    };
}
