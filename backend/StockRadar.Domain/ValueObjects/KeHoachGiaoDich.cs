namespace StockRadar.Domain.ValueObjects;

/// <summary>
/// Kế hoạch giao dịch đầy đủ được tính khi kịch bản TRIGGERED.
/// Bao gồm vùng vào lệnh, dừng lỗ, chốt lời và điều kiện hủy.
/// </summary>
public record KeHoachGiaoDich
{
    /// <summary>Giá vào lệnh tối thiểu</summary>
    public decimal GiaVaoLenhMin { get; init; }

    /// <summary>Giá vào lệnh tối đa</summary>
    public decimal GiaVaoLenhMax { get; init; }

    /// <summary>Giá dừng lỗ (Stop Loss)</summary>
    public decimal GiaDungLo { get; init; }

    /// <summary>Giá chốt lời 1 (Take Profit 1)</summary>
    public decimal GiaChotLoi1 { get; init; }

    /// <summary>Giá chốt lời 2 (Take Profit 2)</summary>
    public decimal GiaChotLoi2 { get; init; }

    /// <summary>Điều kiện hủy kịch bản (mô tả text)</summary>
    public string DieuKienHuy { get; init; } = string.Empty;

    /// <summary>
    /// Tỷ lệ Lãi/Lỗ (R:R) = (TP1 − Entry) / (Entry − SL).
    /// Mẫu số ≤ 0 (SL chưa đặt hoặc sai) → trả 0 thay vì chia lỗi.
    /// </summary>
    public decimal TyLeLaiLo
    {
        get
        {
            var risk = GiaVaoLenhMin - GiaDungLo;
            return risk <= 0 ? 0 : (GiaChotLoi1 - GiaVaoLenhMin) / risk;
        }
    }
}
