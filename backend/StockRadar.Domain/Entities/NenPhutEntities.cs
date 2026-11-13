namespace StockRadar.Domain.Entities;

/// <summary>
/// Nến 1 phút của một mã trong một phiên: mỗi mã mỗi phiên một dòng, khối nến nén gzip ở <see cref="DuLieuNen"/>.
/// Thiết kế bảng, định dạng khối và đơn vị giá: specs/017-intraday-minute-bars/plan.md (D1, D2).
/// </summary>
public class NenPhutTheoPhienEntity
{
    public long Id { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public DateOnly NgayPhien { get; set; }

    public int SoNen { get; set; }

    public string Nguon { get; set; } = "KBS";

    public byte[] DuLieuNen { get; set; } = [];

    public DateTime ThoiDiemGhi { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Sổ chất lượng tải nến phút theo mã và phiên: tách "đủ / thiếu / rỗng / lỗi", ghi giờ nến ATC thật của phiên.
/// Thiết kế bảng và luật phân loại: specs/017-intraday-minute-bars/plan.md (D3).
/// </summary>
public class SoChatLuongNenPhutEntity
{
    public long Id { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public DateOnly NgayPhien { get; set; }

    public int SoNenNhanDuoc { get; set; }

    public string TrangThai { get; set; } = string.Empty;

    public TimeOnly? GioNenAtc { get; set; }

    public int SoLanThu { get; set; }

    public string? ThongDiepLoi { get; set; }

    public string LanGhi { get; set; } = string.Empty;

    public DateTime ThoiDiemGhi { get; set; } = DateTime.UtcNow;
}
