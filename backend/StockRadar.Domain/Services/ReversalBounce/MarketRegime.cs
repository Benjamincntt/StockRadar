namespace StockRadar.Domain.Services.ReversalBounce;

/// <summary>
/// Trạng thái thị trường tổng cho chiến lược counter-trend (ReversalBounce).
/// Hệ độc lập, chạy song song với <c>MarketWyckoffPhase</c> (pro-trend) — không thay thế.
/// </summary>
public enum MarketRegime
{
    /// <summary>Thị trường bình thường — không thuộc panic/stabilizing/rebound.</summary>
    Normal,

    /// <summary>Đang cân bằng sau bán tháo hoặc sau khi rời rebound — giai đoạn chuyển tiếp.</summary>
    Stabilizing,

    /// <summary>Hồi phục đã xác nhận: VN-Index lấy lại MA20 và độ rộng cải thiện.</summary>
    ReboundConfirmed,

    /// <summary>Bán tháo diện rộng — cấm sinh tín hiệu mua counter-trend.</summary>
    Panic
}
