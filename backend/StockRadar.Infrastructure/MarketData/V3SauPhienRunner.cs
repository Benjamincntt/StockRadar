using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Infrastructure.Notifications;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Nhịp V3 SAU PHIÊN (16:00, Phụ lục D) — nhịp NẠP ĐẦY ĐỦ DUY NHẤT trong ngày. Bốn việc:
/// (1) quét một lần toàn universe, tính giá trị chỉ số bình quân đều CỦA HÔM NAY (lợi nhuận TB
///     các mã UNI phiên trước, kẹp ±16% — research 01) và DANH SÁCH UNI cho phiên sau, lưu vào ChiSoNgayV3;
/// (1a) đối chiếu vị thế với lệnh thật trong Nhật ký giao dịch của tài khoản danh mục (spec 014);
/// (2) tính sẵn ngưỡng bán (TB 9 hoặc 19 giá đóng cửa trước) + giá tham chiếu cho vị thế đang giữ, lưu vào ViTheV3;
/// (3) cập nhật số phiên đã giữ. Không bắn tin, không đổi trạng thái thoát (thoát đã xử lý realtime/ATC).
/// </summary>
internal sealed class V3SauPhienRunner(
    V3Engine engine,
    INhatKyRepository nhatKy,
    IOptions<V3Options> options,
    IGuiThongBao guiTin,
    ILogger<V3SauPhienRunner> logger) : IV3SauPhienService
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
            logger.LogInformation("V3 sau phiên: nhịp chạy hết {Ms} ms.", sw.ElapsedMilliseconds);
        }
    }

    private async Task<V3ChayKetQua> ChayBenTrongAsync(CancellationToken ct)
    {
        var homNay = VietnamMarketCalendar.TodayVietnam();
        if (!VietnamMarketCalendar.IsTradingDay(homNay))
            return new V3ChayKetQua(0, 0, 0);

        var cacMa = await engine.LayCacMaAsync(ct);
        if (cacMa.Count == 0)
            return new V3ChayKetQua(0, 0, 0);

        // (1) Chỉ số phiên hôm nay + UNI cho phiên sau. Lúc 16:00 nến hôm nay đã đóng cửa,
        // nên mốc "chưa gồm hôm nay" phải là NGÀY MAI (V3CotNen.NenHoanThanh lấy nến < mốc).
        var ngayMai = VietnamMarketCalendar.NextTradingDay(homNay);
        var kt = await engine.LayChiSoAsync(
            cacMa, ngayMai, quoteGia: null, quoteVolume: null, V3CheDoTai.DayDu, ct);

        var stockMap = new Dictionary<string, Stock>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in cacMa)
            stockMap[s.Symbol] = s;

        // (2) + (3) Chuẩn bị trước cho phiên sau: ngưỡng bán, giá tham chiếu, sàn, số phiên đã giữ.
        var cfg = engine.Config;
        var viTheMo = await engine.LayViTheDangMoAsync(ct);

        // FR-014a: mọi lệnh CHỜ MUA đã tới/sau phiên mua mà chưa khớp → huỷ lúc 16:00 (đặt TRƯỚC cập nhật ngưỡng),
        // bỏ chặn mã, tín hiệu ghi huỷ, bắn tin HUỶ.
        int soHuyCho = 0;
        foreach (var vt in viTheMo.Where(v => v.TrangThai == TrangThaiViTheV3.ChoMua
                     && V3LenhMua.DaDenHoacQuaPhienMua(v.Loai, cfg, v.NgayTinHieu, homNay)))
        {
            var th = await engine.HuyChoMuaAsync(vt, ct);
            if (th is not null)
                guiTin.DatTinV3(LoaiTinV3.HuyMua, vt.Symbol, vt.Loai,
                    V3TelegramFormatter.FormatHuyMua(vt.Symbol, vt.Loai, "không khớp trong phiên mua"));
            soHuyCho++;
            logger.LogInformation("V3 sau phiên HỦY chờ mua (quá phiên mua) — {Symbol} {Loai} (tín hiệu {Ngay}).",
                vt.Symbol, vt.Loai, vt.NgayTinHieu);
        }

        // (1a) Spec 014: đối chiếu với lệnh thật trong Nhật ký — chạy TRƯỚC vòng ngưỡng bán để
        // ngưỡng (và tiền mặt, cỡ lệnh sau này) tính theo giá vào thật.
        await DoiChieuNhatKyAsync(homNay, viTheMo, cfg, ct);

        int soCapNhat = 0;
        foreach (var vt in viTheMo)
        {
            if (vt.TrangThai == TrangThaiViTheV3.BiHuy)
                continue; // vừa huỷ theo FR-014a — không cập nhật nữa
            vt.SoPhienDaGi = V3PhienHoTro.SoPhienDaGi(vt.NgayTinHieu, homNay);
            if (stockMap.TryGetValue(vt.Symbol, out var s))
            {
                // closes tính tới hết phiên hôm nay (nến hôm nay đã đóng cửa lúc 16:00).
                var closes = V3CotNen.NenHoanThanh(s.History, ngayMai).Select(b => b.Close).ToList();
                if (vt.TrangThai == TrangThaiViTheV3.DangGiu)
                {
                    // DO-NOT-CHANGE: lệnh hoảng loạn sâu giữ nguyên ngưỡng giá vào × 1,15 — nhịp 16:00
                    // KHÔNG được ghi đè bằng ngưỡng MA, nếu không lệnh hoảng loạn quay lại bán sớm (FR-015a).
                    if (!vt.LaHoangLoanSau)
                        vt.NguongChamMa = engine.TinhNguongBan(vt.Loai, vt.GiaVao ?? 0m, closes);
                }
                // Đóng cửa hôm nay = giá tham chiếu của phiên sau (trần/sàn + lợi nhuận ngày).
                if (closes.Count > 0)
                    vt.GiaThamChieu = closes[^1];
                vt.Exchange = s.Exchange;
            }
            vt.UpdatedAt = DateTime.UtcNow;
            soCapNhat++;
        }

        await engine.LuuAsync(ct);
        await guiTin.GuiTatCaAsync(ct);
        logger.LogInformation(
            "V3 sau phiên: lưu chỉ số {Ngay} = {ChiSo} (MA50 {Ma50}, lệch {Lech}%), {Uni} mã UNI cho phiên sau {NgayMai}; huỷ {Huy} lệnh chờ mua, cập nhật {Count} vị thế đang mở.",
            kt.NgayTinHieu, kt.ChiSo, kt.ChiSoMa50, kt.ChiSoDoLechMa50PhanTram,
            kt.DanhSachUni.Count, ngayMai, soHuyCho, soCapNhat);
        return new V3ChayKetQua(0, 0, 0);
    }

    /// <summary>
    /// (1a) Đối chiếu vị thế V3 với lệnh thật trong Nhật ký giao dịch của tài khoản danh mục (spec 014).
    /// Thứ tự: chạy sau phần huỷ chờ mua và TRƯỚC vòng tính ngưỡng bán, để ngưỡng (và tiền mặt,
    /// cỡ lệnh các nhịp sau) tính theo giá vào thật.
    /// Thiếu V3:TaiKhoanDanhMuc hoặc sai dạng Guid thì ghi log và bỏ qua bước này; phần còn lại của nhịp vẫn chạy.
    /// </summary>
    /// <param name="homNay">Phiên đang đóng (giờ Việt Nam).</param>
    /// <param name="viTheMo">Vị thế Chờ mua/Đang giữ mà nhịp đang xử lý.</param>
    /// <param name="cfg">Cấu hình V3 thuần cho luật đối chiếu.</param>
    /// <param name="ct">Token hủy nhịp.</param>
    private async Task DoiChieuNhatKyAsync(
        DateOnly homNay, List<ViTheV3Entity> viTheMo, V3CauHinh cfg, CancellationToken ct)
    {
        var chuTaiKhoan = options.Value.TaiKhoanDanhMuc;
        if (string.IsNullOrWhiteSpace(chuTaiKhoan))
        {
            logger.LogInformation("V3 sau phiên: chưa cấu hình V3:TaiKhoanDanhMuc — bỏ bước đối chiếu nhật ký.");
            return;
        }
        if (!Guid.TryParse(chuTaiKhoan, out var taiKhoanDanhMuc))
        {
            logger.LogWarning(
                "V3 sau phiên: V3:TaiKhoanDanhMuc không phải Guid hợp lệ — bỏ bước đối chiếu nhật ký.");
            return;
        }

        // Domain không biết múi giờ: quy đổi ThoiDiemKhop (UTC) sang ngày giờ Việt Nam ngay tại đây.
        var cacLenh = (await nhatKy.LayToanBoLenhAsync(taiKhoanDanhMuc, ct))
            .Select(l => new V3DoiChieuNhatKy.Lenh(
                l.MaChungKhoan,
                l.Chieu,
                DateOnly.FromDateTime(VietnamMarketCalendar.ToVietnam(l.ThoiDiemKhop)),
                l.GiaKhop,
                l.KhoiLuong,
                l.NguonTinHieu,
                l.MaTinHieu))
            .ToList();
        var boQua = (await nhatKy.LayBoQuaTheoNguonAsync(taiKhoanDanhMuc, V3DoiChieuNhatKy.NguonV3, ct))
            .Select(b => b.MaTinHieu)
            .ToHashSet(StringComparer.Ordinal);
        // DO-NOT-CHANGE: chỉ thoát sớm khi CẢ lệnh lẫn bỏ qua đều rỗng — thoát theo riêng lệnh thì
        // tài khoản chưa nhập lệnh nào bấm "bỏ qua" sẽ không huỷ được vị thế, vẫn chiếm tiền và chỗ.
        if (cacLenh.Count == 0 && boQua.Count == 0)
            return;

        // Vị thế DaThoat hôm nay KHÔNG có trong viTheMo (LayViTheDangMoAsync chỉ trả DangGiu + ChoMua).
        var ungVien = viTheMo
            .Where(v => v.TrangThai == TrangThaiViTheV3.DangGiu)
            .Concat(await engine.LayViTheDaThoatTrongNgayAsync(homNay, ct))
            .ToList();

        int soDoiChieu = 0;
        foreach (var vt in ungVien)
        {
            var kq = V3DoiChieuNhatKy.DoiChieu(
                new V3DoiChieuNhatKy.ViThe(
                    vt.TinHieuV3Id, vt.Symbol, vt.TrangThai, vt.NgayVao, vt.GiaVao,
                    vt.KhoiLuong, vt.NgayThoat, vt.LaHoangLoanSau),
                homNay,
                cacLenh,
                boQua.Contains(vt.TinHieuV3Id.ToString()),
                cfg);

            if (kq.SoLenhLechMa > 0)
                logger.LogWarning(
                    "V3 sau phiên: {N} lệnh nhật ký gắn tín hiệu {TinHieu} nhưng mã CK khác vị thế {Symbol} — bỏ qua.",
                    kq.SoLenhLechMa, vt.TinHieuV3Id, vt.Symbol);
            if (kq.BanMotPhan)
                logger.LogWarning(
                    "V3 sau phiên: {Symbol} có lệnh bán một phần trong nhật ký — bản này chỉ áp khi bán hết, giữ số hệ thống.",
                    vt.Symbol);

            if (kq.CoThayDoi && await engine.GhiDoiChieuNhatKyAsync(vt, kq, ct))
                soDoiChieu++;
        }

        logger.LogInformation("V3 sau phiên: đối chiếu nhật ký {N} vị thế.", soDoiChieu);
    }
}
