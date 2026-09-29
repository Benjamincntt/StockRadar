namespace StockRadar.Domain.ValueObjects;

/// <summary>
/// Kết quả Ichimoku Kinko Hyo — hệ thống đường mây Nhật Bản.
/// TenkanSen (9 phiên), KijunSen (26 phiên), SenkouSpanA/B (mây, chiếu 26 phiên),
/// GiaTrenMay = giá đóng cửa nằm trên mây (xu hướng tăng theo Ichimoku).
/// </summary>
public record IchimokuResult(
    decimal TenkanSen,
    decimal KijunSen,
    decimal SenkouSpanA,
    decimal SenkouSpanB,
    bool GiaTrenMay);

/// <summary>
/// Kết quả phân tích SMC (Smart Money Concepts).
/// HasBos = có Break of Structure (giá phá vỡ cấu trúc đỉnh/đáy N phiên);
/// HasLiquiditySweep = có quét thanh khoản (xuyên đáy/đỉnh rồi giành lại);
/// SweepDirection = "Len" (quét đáy rồi close trên) / "Xuong" (quét đỉnh rồi close dưới) / "" (không có).
/// </summary>
public record SmcResult(bool HasBos, bool HasLiquiditySweep, string SweepDirection);
