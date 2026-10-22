namespace StockRadar.Infrastructure.Persistence.Entities;

/// <summary>
/// Lệnh thật khớp lệnh của user (nhật ký giao dịch). Details: specs/010-trade-journal/data-model.md.
/// </summary>
public class NhatKyGiaoDichEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string MaChungKhoan { get; set; } = string.Empty;

    /// <summary>Mua | Ban | DieuChinh</summary>
    public string Chieu { get; set; } = string.Empty;

    public DateTime ThoiDiemKhop { get; set; }

    public decimal GiaKhop { get; set; }

    public decimal KhoiLuong { get; set; }

    public decimal? Phi { get; set; }

    public string? NguonTinHieu { get; set; }

    public string? MaTinHieu { get; set; }

    public decimal? GiaTinHieuChup { get; set; }

    public string? GhiChu { get; set; }

    public string? LyDo { get; set; }

    public decimal? KLieuChinh { get; set; }

    public decimal? VonDieuChinh { get; set; }

    public DateTime TaoNgay { get; set; }

    public DateTime? CapNhatNgay { get; set; }

    public UserEntity User { get; set; } = null!;
}

/// <summary>
/// Tín hiệu user đánh dấu "không khớp / bỏ qua" (Q3). Details: specs/010-trade-journal/data-model.md.
/// </summary>
public class NhatKyTinHieuBoQuaEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>V2 | VIP</summary>
    public string Nguon { get; set; } = string.Empty;

    public string MaTinHieu { get; set; } = string.Empty;

    public string Symbol { get; set; } = string.Empty;

    public string? LyDo { get; set; }

    public DateTime BoQuaNgay { get; set; }

    public UserEntity User { get; set; } = null!;
}
