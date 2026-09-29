using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

/// <summary>
/// Service truy vấn hiệu quả kịch bản V2 (Performance Tracking).
/// Đọc dữ liệu outcome đã được Pha 3 đo lường và tổng hợp cho UI (mobile/web).
/// </summary>
public interface IHieuQuaKichBanService
{
    /// <summary>
    /// Lấy tóm tắt hiệu quả tổng hợp theo kỳ.
    /// </summary>
    /// <param name="period">"week" (7 ngày) / "month" (30 ngày) / "quarter" (90 ngày) / "all" (không lọc).</param>
    Task<HieuQuaTomTatDto> GetTomTatAsync(string period, CancellationToken ct = default);

    /// <summary>
    /// Lấy lịch sử lệnh (kịch bản đã kích hoạt) có phân trang, sắp xếp theo thời điểm kích hoạt giảm dần.
    /// </summary>
    Task<(IReadOnlyList<LichSuLenhDto> Items, int TotalCount)> GetLichSuAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Lấy chi tiết một lệnh kèm snapshot JSON thô lúc kích hoạt. null nếu không tìm thấy.
    /// </summary>
    Task<ChiTietLenhDto?> GetChiTietAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Lấy chi tiết kịch bản V2 của một mã — bản ghi mới nhất (NgayDanhGia lớn nhất) cho mỗi loại kịch bản.
    /// null nếu mã chưa có bản ghi kịch bản nào.
    /// </summary>
    Task<KichBanTheoSymbolDto?> GetKichBanTheoSymbolAsync(string symbol, CancellationToken ct = default);
}
