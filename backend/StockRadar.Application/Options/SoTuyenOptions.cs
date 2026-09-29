namespace StockRadar.Application.Options;

/// <summary>Cấu hình bộ lọc sơ tuyển — giảm ~1500 mã xuống ~70 mã đáng quan tâm</summary>
public class SoTuyenOptions
{
    public const string SectionName = "SoTuyen";

    /// <summary>Giá trị giao dịch trung bình 20 phiên tối thiểu (VND). Mặc định: 10 tỷ</summary>
    public decimal MinGiaTriGiaoDichTrungBinh { get; set; } = 10_000_000_000m;

    /// <summary>Vốn hóa thị trường tối thiểu (VND). Mặc định: 500 tỷ</summary>
    public decimal MinVonHoa { get; set; } = 500_000_000_000m;

    /// <summary>Số phiên lịch sử tối thiểu. Mặc định: 250</summary>
    public int MinSoPhienLichSu { get; set; } = 250;
}
