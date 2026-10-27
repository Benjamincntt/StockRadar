using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Services;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Lần nạp MỘT LẦN khi deploy (owner duyệt): dựng lại chuỗi chỉ số bình quân đều + UNI theo từng
/// phiên từ 01/2023 tới hôm qua và ghi vào <c>ChiSoNgayV3</c>, để các nhịp nhẹ 08:30/14:25 chỉ cần
/// đọc chuỗi đã lưu thay vì quét lại toàn bộ lịch sử.
/// BUSINESS-RULE: KHÔNG tạo tín hiệu, KHÔNG mở vị thế cho các ngày đã qua — chỉ ghi bảng chỉ số.
/// </summary>
internal sealed class V3NapChiSoBackfillRunner(
    V3Engine engine,
    IOptions<V3Options> options,
    ILogger<V3NapChiSoBackfillRunner> logger) : IV3NapChiSoBackfillService
{
    public async Task<V3NapChiSoKetQua> NapAsync(DateOnly? tuNgay = null, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            return await NapBenTrongAsync(tuNgay, ct);
        }
        finally
        {
            sw.Stop();
            logger.LogInformation("V3 nạp chỉ số lịch sử: nhịp chạy hết {Ms} ms.", sw.ElapsedMilliseconds);
        }
    }

    private async Task<V3NapChiSoKetQua> NapBenTrongAsync(DateOnly? tuNgay, CancellationToken ct)
    {
        var batDau = tuNgay ?? ParseTuNgay(options.Value.TuNgayNapChiSo) ?? new DateOnly(2023, 1, 1);
        var homNay = VietnamMarketCalendar.TodayVietnam();

        // Nạp đầy đủ đúng một lần; loại nến hôm nay (chưa đóng cửa) — chuỗi chỉ tính các phiên đã hoàn thành.
        var cacMa = await engine.LayCacMaAsync(ct);
        var chuanHoa = V3CotNen.ChuyenHistory(cacMa, bars => V3CotNen.NenHoanThanh(bars, homNay));

        var chuoi = V3ChiSoToanThiTruong.TinhChuoi(chuanHoa, engine.Config, batDau);
        var soPhien = await engine.GhiChuoiChiSoAsync(chuoi, ct);

        var ketQua = new V3NapChiSoKetQua(
            soPhien,
            chuoi.Count > 0 ? chuoi[0].Ngay : batDau,
            chuoi.Count > 0 ? chuoi[^1].Ngay : batDau,
            chuoi.Count > 0 ? chuoi[^1].SoMaUni : 0);

        logger.LogInformation(
            "V3 nạp chỉ số lịch sử: {SoPhien} phiên từ {Tu} tới {Den}; UNI phiên cuối {Uni} mã, chỉ số {ChiSo}.",
            ketQua.SoPhienDaLuu, ketQua.TuNgay, ketQua.DenNgay, ketQua.SoMaUniPhienCuoi,
            chuoi.Count > 0 ? chuoi[^1].ChiSo : 0m);
        return ketQua;
    }

    private static DateOnly? ParseTuNgay(string? giaTri) =>
        DateOnly.TryParse(giaTri, out var ngay) ? ngay : null;
}
