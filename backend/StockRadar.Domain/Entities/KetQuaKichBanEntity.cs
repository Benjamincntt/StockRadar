using StockRadar.Domain.Enums;

namespace StockRadar.Domain.Entities;

/// <summary>
/// Kết quả đánh giá kịch bản cho một cổ phiếu tại một thời điểm.
/// Mỗi mã có thể có nhiều bản ghi (một cho mỗi kịch bản đang theo dõi).
/// </summary>
public class KetQuaKichBanEntity
{
    public long Id { get; set; }

    /// <summary>Mã cổ phiếu (ví dụ: "HPG")</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Loại kịch bản</summary>
    public LoaiKichBan LoaiKichBan { get; set; }

    /// <summary>Trạng thái hiện tại</summary>
    public TrangThaiKichBan TrangThai { get; set; }

    /// <summary>Đạt bối cảnh không?</summary>
    public bool DatBoiCanh { get; set; }

    /// <summary>Đạt hình thái không?</summary>
    public bool DatHinhThai { get; set; }

    /// <summary>Đạt cò kích hoạt không?</summary>
    public bool DatCoKichHoat { get; set; }

    /// <summary>Mức hoàn thiện 0-100%</summary>
    public decimal MucHoanThien { get; set; }

    /// <summary>Ngày đánh giá (phiên giao dịch)</summary>
    public DateTime NgayDanhGia { get; set; }

    /// <summary>Thời điểm kích hoạt (null nếu chưa kích hoạt)</summary>
    public DateTime? ThoiGianKichHoat { get; set; }

    /// <summary>Kế hoạch giao dịch (JSON serialized) — chỉ có khi TRIGGERED</summary>
    public string? KeHoachGiaoDichJson { get; set; }

    /// <summary>Bản chụp chỉ báo (JSON serialized) — chỉ có khi TRIGGERED</summary>
    public string? BangChupChiBaoJson { get; set; }

    /// <summary>Danh sách bằng chứng (JSON serialized)</summary>
    public string? DanhSachBangChungJson { get; set; }

    /// <summary>Điểm xếp hạng cơ hội (0-100) — chỉ có khi TRIGGERED + đã xếp hạng</summary>
    public decimal? DiemXepHang { get; set; }

    // === Outcome (điền sau T+1/T+2/T+3) ===

    /// <summary>Lợi nhuận T+1 (%)</summary>
    public decimal? LoiNhuanT1 { get; set; }

    /// <summary>Lợi nhuận T+2 (%)</summary>
    public decimal? LoiNhuanT2 { get; set; }

    /// <summary>Lợi nhuận T+3 (%)</summary>
    public decimal? LoiNhuanT3 { get; set; }

    /// <summary>MFE — lãi cao nhất đạt được (%)</summary>
    public decimal? Mfe { get; set; }

    /// <summary>MAE — lỗ sâu nhất (%)</summary>
    public decimal? Mae { get; set; }

    /// <summary>Ngày tạo bản ghi</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Ngày cập nhật cuối</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
