namespace StockRadar.Application.Abstractions;

/// <summary>
/// Nguồn dữ liệu phục vụ xếp hạng cơ hội V2.
/// Cung cấp RS percentile, trạng thái sóng ngành và pha thị trường cho từng mã.
/// Tách riêng để <c>XepHangCoHoiService</c> không phụ thuộc cứng vào repository/engine cụ thể.
/// </summary>
public interface INguonDuLieuXepHang
{
    /// <summary>Lấy RS percentile (0-100) cho một mã. null nếu không có data.</summary>
    Task<decimal?> LayRsPercentileAsync(string symbol, CancellationToken ct = default);

    /// <summary>
    /// Lấy trạng thái sóng ngành cho sector của mã ("Active" / "Emerging" / "None").
    /// null nếu không xác định được.
    /// </summary>
    Task<string?> LayTrangThaiNganhAsync(string symbol, CancellationToken ct = default);

    /// <summary>Lấy pha thị trường hiện tại ("Favorable" / "Neutral" / "Unfavorable").</summary>
    Task<string> LayPhaThiTruongAsync(CancellationToken ct = default);
}
