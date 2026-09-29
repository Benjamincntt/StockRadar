using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

/// <summary>
/// Kết quả đánh giá một kịch bản cho một mã — domain object (không phải DB entity).
/// Chứa đầy đủ thông tin về trạng thái, evidence, kế hoạch giao dịch.
/// </summary>
public class KetQuaKichBan
{
    public string Symbol { get; set; } = string.Empty;
    public LoaiKichBan LoaiKichBan { get; set; }
    public TrangThaiKichBan TrangThai { get; set; }
    public bool DatBoiCanh { get; set; }
    public bool DatHinhThai { get; set; }
    public bool DatCoKichHoat { get; set; }

    /// <summary>Mức hoàn thiện: 0% = chưa đạt gì, 33% = bối cảnh, 67% = +hình thái, 100% = trigger</summary>
    public decimal MucHoanThien => TrangThai switch
    {
        TrangThaiKichBan.DaKichHoat => 100m,
        TrangThaiKichBan.DangHinhThanh => 67m,
        TrangThaiKichBan.DangTheoDoi when DatBoiCanh => 33m,
        _ => 0m
    };

    public KeHoachGiaoDich? KeHoach { get; set; }
    public BangChupChiBao? BangChup { get; set; }
    public List<BangChung> DanhSachBangChung { get; set; } = new();
    public DateTime? ThoiGianKichHoat { get; set; }
    public decimal? DiemXepHang { get; set; }
}

/// <summary>Kết quả đánh giá một vai trò (Bối cảnh / Hình thái / Cò kích hoạt)</summary>
public record KetQuaVaiTro(bool Dat, List<BangChung> BangChungs);

/// <summary>Kết quả sơ tuyển — danh sách mã đạt tiêu chuẩn + thống kê</summary>
public record KetQuaSoTuyen(
    IReadOnlyList<string> DanhSachMa,
    int TongSoMaDauVao,
    int SoMaLoaiThanhKhoan,
    int SoMaLoaiVonHoa,
    int SoMaLoaiLichSu,
    int SoMaLoaiHanChe);

/// <summary>Kết quả xếp hạng cho một kịch bản đã trigger</summary>
public record KetQuaXepHang(
    KetQuaKichBan KetQua,
    decimal DiemTong,
    decimal DiemRs,
    decimal DiemSector,
    decimal DiemTrigger,
    decimal DiemRegime,
    decimal DiemTyLeLaiLo,
    decimal DiemConfluence);
