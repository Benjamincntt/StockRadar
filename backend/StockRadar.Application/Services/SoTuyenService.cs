using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

/// <summary>
/// Service sơ tuyển — lọc toàn bộ universe (~1500 mã) xuống tập đáng quan tâm (~70 mã)
/// trước khi Scenario Engine V2 đánh giá kịch bản.
/// Tiêu chí: lịch sử ≥ 250 phiên, giá trị giao dịch TB20 ≥ 10 tỷ VND, không hạn chế giao dịch.
/// </summary>
public sealed class SoTuyenService(
    IJobStockRepository stocks,
    IOptions<SoTuyenOptions> options) : ISoTuyenService
{
    /// <summary>Số phiên dùng để tính giá trị giao dịch trung bình (VND).</summary>
    private const int ThanhKhoanLookback = 20;

    public async Task<KetQuaSoTuyen> ChaySoTuyenAsync(CancellationToken ct = default)
    {
        var cfg = options.Value;

        // Lấy TẤT CẢ mã có lịch sử (kể cả inactive) — sơ tuyển tự quyết định mã nào đáng quan tâm,
        // không phụ thuộc trạng thái universe do Job 1 đóng băng.
        IReadOnlyList<Stock> tatCaMa = await stocks.GetAllForUniverseScreeningAsync(ct);

        var danhSachDat = new List<string>();
        int loaiThanhKhoan = 0, loaiVonHoa = 0, loaiLichSu = 0, loaiHanChe = 0;

        foreach (var stock in tatCaMa)
        {
            // 1. Mã bị hạn chế giao dịch (đình chỉ, cảnh báo) → loại ngay.
            if (stock.TradingRestricted)
            {
                loaiHanChe++;
                continue;
            }

            // 2. Lịch sử tối thiểu — thiếu dữ liệu thì chỉ báo (EMA200, Ichimoku 52...) không tin cậy.
            if (stock.History.Count < cfg.MinSoPhienLichSu)
            {
                loaiLichSu++;
                continue;
            }

            // 3. Thanh khoản — giá trị khớp TB20 (VND). Close lưu theo nghìn VND nên
            //    AverageTurnoverValue đã nhân 1000 để ra VND đầy đủ.
            var giaTriTrungBinh = IndicatorMath.AverageTurnoverValue(stock.History, ThanhKhoanLookback);
            if (giaTriTrungBinh < cfg.MinGiaTriGiaoDichTrungBinh)
            {
                loaiThanhKhoan++;
                continue;
            }

            // 4. Vốn hóa — Stock hiện chưa có số liệu vốn hóa/số CP lưu hành trong hệ thống.
            //    Tiêu chí MinVonHoa được giữ trong config để bật ngay khi có nguồn dữ liệu;
            //    cho đến lúc đó không loại mã nào vì vốn hóa (loaiVonHoa luôn = 0).

            danhSachDat.Add(stock.Symbol);
        }

        return new KetQuaSoTuyen(
            danhSachDat,
            tatCaMa.Count,
            loaiThanhKhoan,
            loaiVonHoa,
            loaiLichSu,
            loaiHanChe);
    }
}
