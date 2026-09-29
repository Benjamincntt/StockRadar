using StockRadar.Domain.Services;

namespace StockRadar.Application.Abstractions;

/// <summary>
/// Service sơ tuyển — lọc ~1500 mã xuống ~70 mã đáng quan tâm nhất.
/// Tiêu chí: thanh khoản ≥ 10 tỷ, vốn hóa ≥ 500 tỷ, lịch sử ≥ 250 phiên, không hạn chế GD.
/// </summary>
public interface ISoTuyenService
{
    /// <summary>
    /// Chạy sơ tuyển, trả về danh sách mã đạt tiêu chuẩn.
    /// </summary>
    Task<KetQuaSoTuyen> ChaySoTuyenAsync(CancellationToken ct = default);
}
