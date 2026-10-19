namespace StockRadar.Domain.ValueObjects;

/// <summary>
/// Kế hoạch giao dịch đầy đủ, chốt một lần lúc kích hoạt và không tính lại sau đó.
/// Trường + mặc định + ràng buộc hợp lệ: specs/009-v2-trade-plan/data-model.md.
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

    public DateTime HetHanVao { get; init; }

    public decimal TyLeBanChotLoi1 { get; init; } = 0.5m;

    public bool DoiDungLoVeGiaVao { get; init; } = true;

    public bool CoDungLoDuoi { get; init; }

    public decimal? KhoangCachDuoi { get; init; }

    public int SoPhienDungTheoThoiGian { get; init; } = 5;

    public decimal NguongLaiToiThieuPhanTram { get; init; } = 3m;

    public int SoPhienToiDa { get; init; } = 20;

    public bool LaDuLieuBoSung { get; init; }

    /// <summary>
    /// Tỷ lệ Lãi/Lỗ kỳ vọng = (CL1 − Max) / (Max − SL), tính từ biên trên vùng vào.
    /// Mẫu số ≤ 0 (SL chưa đặt hoặc sai) → trả 0 thay vì chia lỗi.
    /// </summary>
    public decimal TyLeLaiLo
    {
        get
        {
            var risk = GiaVaoLenhMax - GiaDungLo;
            return risk <= 0 ? 0 : (GiaChotLoi1 - GiaVaoLenhMax) / risk;
        }
    }
}
