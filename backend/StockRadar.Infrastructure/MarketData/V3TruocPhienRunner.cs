using Microsoft.Extensions.Logging;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Nhịp V3 TRƯỚC PHIÊN (08:30, Phụ lục D) — nhịp NHẸ: chỉ nạp lịch sử các mã trong UNI đã lưu
/// (~200 mã, do nhịp 16:00 ghi vào ChiSoNgayV3), MA50 của chỉ số đọc từ chuỗi đã lưu (không cộng
/// điểm hôm nay vì phiên tín hiệu là phiên qua). Chấm KB1/KB2 trên giá đóng cửa phiên qua (T)
/// → chọn lệnh theo danh mục → ghi tín hiệu + vị thế CHỜ MUA (khớp ATO mở cửa hôm nay T+1)
/// → bắn Telegram. Đồng thời nhắc ATO bán N3 đúng phiên TDM=2 tháng sau.
/// </summary>
internal sealed class V3TruocPhienRunner(
    V3Engine engine,
    ITelegramNotifier telegram,
    ILogger<V3TruocPhienRunner> logger) : IV3TruocPhienService
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
            logger.LogInformation("V3 trước phiên: nhịp chạy hết {Ms} ms.", sw.ElapsedMilliseconds);
        }
    }

    private async Task<V3ChayKetQua> ChayBenTrongAsync(CancellationToken ct)
    {
        var homNay = VietnamMarketCalendar.TodayVietnam();
        if (!VietnamMarketCalendar.IsTradingDay(homNay))
        {
            logger.LogDebug("V3 trước phiên: bỏ qua — {Ngay} không phải ngày giao dịch.", homNay);
            return new V3ChayKetQua(0, 0, 0);
        }

        var cfg = engine.Config;
        var cacMa = await engine.LayMaNhipNheAsync(homNay, ct);
        if (cacMa.Count == 0)
            return new V3ChayKetQua(0, 0, 0);

        var stockMap = new Dictionary<string, Stock>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in cacMa)
            stockMap[s.Symbol] = s;

        // quoteGia null → chỉ báo tính trên nến HOÀN THÀNH tới hôm qua (phiên tín hiệu T).
        var kt = await engine.LayChiSoAsync(
            cacMa, homNay, quoteGia: null, quoteVolume: null, V3CheDoTai.NhipNhe, ct);
        var ungVienMoCua = V3BoKichBan.Cham(kt, cfg)
            .Where(u => u.MuaMoCuaPhienSau)
            .ToList();

        // Nhắc ATO bán N3 khi hôm nay là phiên TDM=2 tháng sau của mã N3 đang giữ.
        var soLenhThoat = 0;
        if (V3LichPhien.LaPhienAtoN3(homNay))
        {
            var viThe = await engine.LayViTheDangMoAsync(ct);
            foreach (var vt in viThe.Where(x => x.Loai == LoaiKichBanV3.N3
                                                 && x.TrangThai == TrangThaiViTheV3.DangGiu))
            {
                var msg = V3TelegramFormatter.FormatAtoN3(
                    vt.Symbol, vt.GiaVao ?? 0m,
                    V3PhienHoTro.SoPhienDaGi(vt.NgayTinHieu, homNay), cfg.GiuToiDaCua(vt.Loai));
                await telegram.SendAsync(msg, ct, TelegramNotifier.HtmlParseMode);
                logger.LogInformation("V3 nhắc ATO bán N3 — {Symbol}.", vt.Symbol);
            }
        }

        // Chọn lệnh theo danh mục (chưa có giá khớp → dùng giá vào làm tham chiếu cho vị thế đang giữ).
        var danhMuc = await engine.LayTrangThaiDanhMucAsync(new Dictionary<string, decimal>(), ct);
        var lenhChon = V3DanhMucXepHang.Chon(ungVienMoCua, danhMuc, cfg);

        int soTinHieu = 0, soLenhMo = 0;
        foreach (var lenh in lenhChon)
        {
            ct.ThrowIfCancellationRequested();
            var uv = ungVienMoCua.First(u => u.Symbol == lenh.Symbol && u.Loai == lenh.Loai);
            var snapshot = kt.TheoMa.TryGetValue(lenh.Symbol, out var c)
                ? V3Engine.ChupSnapshot(c, kt)
                : "{}";

            var th = await engine.GhiTinHieuAsync(uv, kt, snapshot, ct);
            soTinHieu++;

            // Cờ hoảng loạn sâu chốt MỘT LẦN từ BREADTH50 phiên tín hiệu T (đã có ở nhịp 08:30 — FR-015a).
            var laHoangLoan = V3LuatBanThucTe.LaHoangLoanSau(lenh.Loai, kt.Breadth50, cfg);

            stockMap.TryGetValue(lenh.Symbol, out var maLenh);
            engine.TaoChoMua(
                th, lenh.GiaTriLenh, maLenh?.Exchange,
                maLenh is null ? 0m : V3PhienHoTro.GiaThamChieu(maLenh, homNay), laHoangLoan);
            soLenhMo++;

            var tv20 = kt.TheoMa.TryGetValue(lenh.Symbol, out var chiTieu) ? chiTieu.Tv20Ty : 0m;
            var tin = V3TelegramFormatter.FormatMua(
                lenh.Loai, lenh.Symbol, uv.GiaTinHieu, tv20, uv.GiaTriThuocXepHang,
                lenh.HangTrongKichBan, cfg.TopMoiKichBan, kt.Breadth50, kt.ChiSoDoLechMa50PhanTram,
                lenh.HeSoTrongSo, lenh.GiaTriLenh / 1_000_000m,
                moCuaPhienSau: true, V3LuatBanThucTe.GiuToiDaTheoViThe(cfg, lenh.Loai, laHoangLoan),
                ngayAtoN3: null, cfg: cfg, laHoangLoanSau: laHoangLoan);
            await telegram.SendAsync(tin, ct, TelegramNotifier.HtmlParseMode);
            logger.LogInformation("V3 MUA (ATO phiên sau) — {Symbol} {Loai} hạng {Hang}.",
                lenh.Symbol, lenh.Loai, lenh.HangTrongKichBan);
        }

        await engine.LuuAsync(ct);
        logger.LogInformation("V3 trước phiên: {Tin} tín hiệu, {Lenh} lệnh chờ mua (nạp {SoMa} mã UNI đã lưu).",
            soTinHieu, soLenhMo, cacMa.Count);
        return new V3ChayKetQua(soTinHieu, soLenhMo, soLenhThoat);
    }
}
