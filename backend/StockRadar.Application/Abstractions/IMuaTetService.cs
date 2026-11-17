using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

/// <summary>
/// Kịch bản "Mùa Tết — mã khỏe" (P4-19, sổ V4) — đợt 1: chỉ tính giỏ từ nến ngày đã lưu và bắn tin.
/// Không ghi bảng, không theo dõi giỏ hằng ngày (đợt 2 chờ owner duyệt vì có migration).
/// </summary>
public interface IMuaTetService
{
    /// <summary>Tính giỏ 10 mã từ nến ngày trong DB (chỉ đọc, không gửi tin).</summary>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>Giỏ kèm số liệu từng mã và ngày dữ liệu đã dùng.</returns>
    Task<MuaTetGioDto> LayGioAsync(CancellationToken ct = default);

    /// <summary>Tính giỏ rồi gửi MỘT tin qua kênh thông báo V3.</summary>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>Đúng giỏ đã gửi trong tin.</returns>
    Task<MuaTetGioDto> BaoTinAsync(CancellationToken ct = default);
}
