using Microsoft.Extensions.Logging;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Nhịp V3 ATC (~14:25, Phụ lục D) — nhịp NHẸ: chỉ nạp lịch sử + giá realtime cho các mã trong
/// UNI đã lưu (~200 mã, cộng thêm mã đang giữ), chỉ số/MA50 đọc từ chuỗi đã lưu và CỘNG ĐIỂM HÔM NAY
/// (ghép nến hôm nay với khối lượng khớp tích lũy, không truyền 0). Chấm KB4/N1/N3 bằng giá khớp
/// hiện tại (tín hiệu = phiên hôm nay), mua ATC ngay phiên tín hiệu (hủy nếu giá trần).
/// Đồng thời xử lý vị thế ĐANG GIỮ hết hạn giữ → bán ATC.
/// </summary>
internal sealed class V3AtcRunner(
    V3Engine engine,
    ITelegramNotifier telegram,
    ILogger<V3AtcRunner> logger) : IV3AtcService
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
            logger.LogInformation("V3 ATC: nhịp chạy hết {Ms} ms.", sw.ElapsedMilliseconds);
        }
    }

    private async Task<V3ChayKetQua> ChayBenTrongAsync(CancellationToken ct)
    {
        var homNay = VietnamMarketCalendar.TodayVietnam();
        // Nhịp ATC 14:25 thuộc buổi chiều, nhưng vẫn lấy giờ thật để đúng luật T+2.5 khi chạy thủ công.
        var gioXet = TimeOnly.FromDateTime(VietnamMarketCalendar.NowVietnam());
        if (!VietnamMarketCalendar.IsTradingDay(homNay))
            return new V3ChayKetQua(0, 0, 0);

        var cfg = engine.Config;
        // Nạp đúng tập UNI đã lưu cho phiên hôm nay (~200 mã) — không quét toàn universe.
        var cacMa = await engine.LayMaNhipNheAsync(homNay, ct);
        if (cacMa.Count == 0)
            return new V3ChayKetQua(0, 0, 0);

        var stockMap = BuildStockMap(cacMa);
        var viThe = await engine.LayViTheDangMoAsync(ct);

        // Lấy giá cho tập UNI + cả mã đang giữ (mã có thể đã rớt UNI nhưng vẫn phải xét bán ATC).
        var symbols = stockMap.Keys.ToList();
        var seen = new HashSet<string>(symbols, StringComparer.OrdinalIgnoreCase);
        foreach (var vt in viThe)
            if (seen.Add(vt.Symbol))
                symbols.Add(vt.Symbol);
        var (gia, volume, high, low) = await engine.LayRealtimeQuotesAsync(symbols, ct);
        // Ghép giá khớp + khối lượng khớp tích lũy hôm nay → nến tín hiệu = hôm nay.
        var kt = await engine.LayChiSoAsync(cacMa, homNay, quoteGia: gia, quoteVolume: volume, V3CheDoTai.NhipNhe, ct);

        var ungVienAtc = V3BoKichBan.Cham(kt, cfg)
            .Where(u => !u.MuaMoCuaPhienSau)
            .ToList();
        var danhMuc = await engine.LayTrangThaiDanhMucAsync(gia, ct);
        var lenhChon = V3DanhMucXepHang.Chon(ungVienAtc, danhMuc, cfg);

        int soTinHieu = 0, soLenhMo = 0, soLenhThoat = 0;
        var ngayAtoN3 = V3LichPhien.NgayAtoThangSau(kt.NgayTinHieu)?.ToString("dd/MM");

        foreach (var lenh in lenhChon)
        {
            ct.ThrowIfCancellationRequested();
            var uv = ungVienAtc.First(u => u.Symbol == lenh.Symbol && u.Loai == lenh.Loai);
            var snapshot = kt.TheoMa.TryGetValue(lenh.Symbol, out var chiTieu)
                ? V3Engine.ChupSnapshot(chiTieu, kt)
                : "{}";
            var th = await engine.GhiTinHieuAsync(uv, kt, snapshot, ct);
            soTinHieu++;

            stockMap.TryGetValue(lenh.Symbol, out var s);
            gia.TryGetValue(lenh.Symbol, out var gk);
            var refP = s is null ? 0m : V3PhienHoTro.GiaThamChieu(s, homNay);
            if (s is not null && gk > 0m && V3PhienHoTro.LaGiaTran(gk, refP, s.Exchange))
            {
                th.TrangThai = TrangThaiTinHieuV3.BiHuy;
                th.UpdatedAt = DateTime.UtcNow;
                logger.LogInformation("V3 ATC HỦY mua (giá trần) — {Symbol} {Loai}.", lenh.Symbol, lenh.Loai);
                continue;
            }

            // Cờ hoảng loạn sâu chốt MỘT LẦN từ BREADTH50 nhịp ATC vừa tính (giá khớp lúc ~14:25 — FR-015a).
            var laHoangLoan = V3LuatBanThucTe.LaHoangLoanSau(lenh.Loai, kt.Breadth50, cfg);

            if (gk > 0m)
            {
                var kl = V3Engine.KhoiLuongTheoLo(lenh.GiaTriLenh, gk);
                if (kl > 0m)
                {
                    // Lưu sẵn ngưỡng bán cho phiên sau (nhịp 16:00 sẽ chốt lại theo nến đóng cửa hôm nay).
                    var nguong = s is null
                        ? (laHoangLoan ? gk * cfg.HoangLoanSauChotLoi : gk * cfg.BanPhanTramGiaVao)
                        : engine.TinhNguongBan(lenh.Loai, gk,
                            V3CotNen.NenHoanThanh(s.History, homNay).Select(b => b.Close).ToList(), laHoangLoan) ?? 0m;
                    engine.MoViThe(th, homNay, gk, kl, gk * kl, s?.Exchange, refP, nguong, laHoangLoan);
                    soLenhMo++;
                }
                else
                {
                    engine.TaoChoMua(th, lenh.GiaTriLenh, s?.Exchange, refP, laHoangLoan);
                }
            }
            else
            {
                // Chưa có giá khớp — ghi chờ mua để nhịp kế khớp.
                engine.TaoChoMua(th, lenh.GiaTriLenh, s?.Exchange, refP, laHoangLoan);
            }

            var tv20 = kt.TheoMa.TryGetValue(lenh.Symbol, out var c2) ? c2.Tv20Ty : 0m;
            var tin = V3TelegramFormatter.FormatMua(
                lenh.Loai, lenh.Symbol, uv.GiaTinHieu, tv20, uv.GiaTriThuocXepHang,
                lenh.HangTrongKichBan, cfg.TopMoiKichBan, kt.Breadth50, kt.ChiSoDoLechMa50PhanTram,
                lenh.HeSoTrongSo, lenh.GiaTriLenh / 1_000_000m,
                moCuaPhienSau: false, V3LuatBanThucTe.GiuToiDaTheoViThe(cfg, lenh.Loai, laHoangLoan),
                lenh.Loai == LoaiKichBanV3.N3 ? ngayAtoN3 : null, cfg: cfg, laHoangLoanSau: laHoangLoan);
            await telegram.SendAsync(tin, ct, TelegramNotifier.HtmlParseMode);
            logger.LogInformation("V3 MUA ATC — {Symbol} {Loai} hạng {Hang}.",
                lenh.Symbol, lenh.Loai, lenh.HangTrongKichBan);
        }

        // Vị thế ĐANG GIỮ hết hạn giữ → bán ATC tại giá hôm nay (qua T+2.5, không sàn trắng).
        foreach (var vt in viThe.Where(v => v.TrangThai == TrangThaiViTheV3.DangGiu))
        {
            if (!gia.TryGetValue(vt.Symbol, out var gk) || gk <= 0m)
                continue;

            var refAtc = vt.GiaThamChieu is > 0m ? vt.GiaThamChieu.Value
                : stockMap.TryGetValue(vt.Symbol, out var sHist) ? V3PhienHoTro.GiaThamChieu(sHist, homNay) : 0m;
            var sanTrang = refAtc > 0m && V3PhienHoTro.LaSanTrang(
                gk, high.GetValueOrDefault(vt.Symbol), low.GetValueOrDefault(vt.Symbol), refAtc, vt.Exchange);
            var soPhien = V3PhienHoTro.SoPhienDaGi(vt.NgayTinHieu, homNay);
            var daQua = V3PhienHoTro.DaQuaTplus25(vt.NgayVao, homNay, gioXet, cfg);
            var nguong = vt.NguongChamMa ?? (vt.GiaVao ?? 0m) * cfg.BanPhanTramGiaVao;

            var kq = V3LuatBanThucTe.XetTheoNguongDaLuu(
                vt.Loai, nguong, gk, soPhien, V3LuatBanThucTe.GiuToiDaTheoViThe(cfg, vt.Loai, vt.LaHoangLoanSau),
                denLichAtoN3: false, daQua, sanTrang, vt.LaHoangLoanSau);

            if (kq.Thoat && kq.LyDo == V3LuatBanThucTe.LyDoThoat.HetHanATC)
            {
                await engine.KhopBanAsync(vt, homNay, gk, kq.LyDo.ToString(), ct);
                var msg = V3TelegramFormatter.FormatBan(
                    vt.Loai, vt.Symbol, gk, vt.GiaVao ?? 0m, soPhien,
                    V3LuatBanThucTe.GiuToiDaTheoViThe(cfg, vt.Loai, vt.LaHoangLoanSau),
                    "Hết hạn giữ (ATC)");
                await telegram.SendAsync(msg, ct, TelegramNotifier.HtmlParseMode);
                soLenhThoat++;
                logger.LogInformation("V3 BÁN ATC (hết hạn) — {Symbol} {Loai} @ {Gia}.", vt.Symbol, vt.Loai, gk);
            }
        }

        await engine.LuuAsync(ct);
        logger.LogInformation(
            "V3 ATC: {Tin} tín hiệu, {Mo} lệnh mua, {Thoat} lệnh thoát (nạp {SoMa} mã UNI đã lưu, chỉ số {ChiSo}).",
            soTinHieu, soLenhMo, soLenhThoat, cacMa.Count, kt.ChiSo);
        return new V3ChayKetQua(soTinHieu, soLenhMo, soLenhThoat);
    }

    private static Dictionary<string, Stock> BuildStockMap(IReadOnlyList<Stock> cacMa)
    {
        var map = new Dictionary<string, Stock>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in cacMa)
            map[s.Symbol] = s;
        return map;
    }
}
