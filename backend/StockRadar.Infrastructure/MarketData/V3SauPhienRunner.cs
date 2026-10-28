using Microsoft.Extensions.Logging;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Nhịp V3 SAU PHIÊN (16:00, Phụ lục D) — nhịp NẠP ĐẦY ĐỦ DUY NHẤT trong ngày. Ba việc:
/// (1) quét một lần toàn universe, tính giá trị chỉ số bình quân đều CỦA HÔM NAY (lợi nhuận TB
///     các mã UNI phiên trước, kẹp ±16% — research 01) và DANH SÁCH UNI cho phiên sau, lưu vào ChiSoNgayV3;
/// (2) tính sẵn ngưỡng bán (TB 9 hoặc 19 giá đóng cửa trước) + giá tham chiếu cho vị thế đang giữ, lưu vào ViTheV3;
/// (3) cập nhật số phiên đã giữ. Không bắn tin, không đổi trạng thái thoát (thoát đã xử lý realtime/ATC).
/// </summary>
internal sealed class V3SauPhienRunner(
    V3Engine engine,
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
        var viTheMo = await engine.LayViTheDangMoAsync(ct);
        int soCapNhat = 0;
        foreach (var vt in viTheMo)
        {
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
        logger.LogInformation(
            "V3 sau phiên: lưu chỉ số {Ngay} = {ChiSo} (MA50 {Ma50}, lệch {Lech}%), {Uni} mã UNI cho phiên sau {NgayMai}; cập nhật {Count} vị thế đang mở.",
            kt.NgayTinHieu, kt.ChiSo, kt.ChiSoMa50, kt.ChiSoDoLechMa50PhanTram,
            kt.DanhSachUni.Count, ngayMai, soCapNhat);
        return new V3ChayKetQua(0, 0, 0);
    }
}
