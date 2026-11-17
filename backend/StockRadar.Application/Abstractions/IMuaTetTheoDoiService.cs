using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

/// <summary>
/// Theo dõi giỏ "Mùa Tết — mã khỏe" (P4-19) ĐÃ MUA: đọc giỏ từ cấu hình + giá hiện tại từ DB,
/// báo cảnh báo sớm (≤ 95% vốn) hoặc bán cả giỏ (≤ 90% vốn). Không ghi bảng, không đụng V3.
/// </summary>
public interface IMuaTetTheoDoiService
{
    /// <summary>Trạng thái hiện tại của giỏ — chỉ đọc, không gửi tin.</summary>
    Task<MuaTetTheoDoiDto> LayTrangThaiAsync(CancellationToken cancellationToken = default);

    /// <summary>Kiểm giỏ và gửi tin khi chạm ngưỡng (mỗi mức tối đa 1 lần/ngày).</summary>
    /// <param name="boQuaChanLap">Bỏ qua chặn gửi lặp — dùng cho lần thử thủ công.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    Task<MuaTetTheoDoiDto> KiemVaBaoAsync(
        bool boQuaChanLap = false, CancellationToken cancellationToken = default);
}
