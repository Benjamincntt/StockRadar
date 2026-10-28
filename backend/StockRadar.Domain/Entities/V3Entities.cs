using StockRadar.Domain.Enums;

namespace StockRadar.Domain.Entities;

/// <summary>
/// Trạng thái vòng đời một TÍN HIỆU V3 (khác trạng thái vị thế). FR-022/Key Entities.
/// </summary>
public enum TrangThaiTinHieuV3
{
    /// <summary>Đã báo (đạt điều kiện + top-N).</summary>
    DaBao = 0,
    /// <summary>Bị cắt vì ràng buộc danh mục (hết chỗ / hết tiền mặt).</summary>
    BiCatDanhMuc = 1,
    /// <summary>Đã vào lệnh (có vị thế tương ứng).</summary>
    DaVaoLenh = 2,
    /// <summary>Hủy — giá mua rơi đúng giá trần (FR-014).</summary>
    BiHuy = 3,
}

/// <summary>
/// Tín hiệu V3: một lần một kịch bản báo trên một mã tại một phiên (Phụ lục G bảng <c>TinHieuV3</c>).
/// Chụp snapshot đủ để giải thích (FR-024). Mirrors giá vào/thoát khi có vị thế để picker nhật ký
/// đọc từ đúng bảng này (FR-040).
/// </summary>
public class TinHieuV3Entity
{
    public long Id { get; set; }

    public string Symbol { get; set; } = string.Empty;

    public LoaiKichBanV3 Loai { get; set; }

    /// <summary>Phiên tín hiệu T (ngày nến cuối dùng để chấm).</summary>
    public DateOnly NgayTinHieu { get; set; }

    /// <summary>Giá tham chiếu/khớp tại phiên tín hiệu (nghìn đồng).</summary>
    public decimal GiaTinHieu { get; set; }

    /// <summary>Hạng trong kịch bản (1 = tốt nhất theo thước xếp hạng).</summary>
    public int Hang { get; set; }

    /// <summary>Giá trị thước xếp hạng (RS126/RSI14/RET20/SECR20/RS63 tùy kịch bản).</summary>
    public decimal GiaTriXepHang { get; set; }

    /// <summary>Nhịp mua: true = ATO/mở cửa phiên sau (KB1/KB2); false = ATC phiên tín hiệu.</summary>
    public bool MuaMoCuaPhienSau { get; set; }

    public TrangThaiTinHieuV3 TrangThai { get; set; }

    /// <summary>Snapshot chỉ báo quyết định (JSON) — TV20/RS/BREADTH50/độ lệch MA50/FLOOR20/LEFT/TDM (FR-024).</summary>
    public string? SnapshotJson { get; set; }

    // === Mirror từ vị thế khi khớp (để nhật ký đọc từ TinHieuV3 — FR-040) ===
    public decimal? GiaVao { get; set; }
    public decimal? GiaThoat { get; set; }
    /// <summary>Lãi ròng % mirror từ vị thế khi đóng (null khi chưa đóng).</summary>
    public decimal? PhanTramLoiNhuan { get; set; }
    /// <summary>Kết quả đã đóng: "Thang"/"Thua"/"HoaVon" (null khi đang giữ).</summary>
    public string? KetQua { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Vị thế V3: lệnh đang giữ hoặc đã đóng (Phụ lục G bảng <c>ViTheV3</c>).
/// </summary>
public class ViTheV3Entity
{
    public long Id { get; set; }

    /// <summary>FK → <see cref="TinHieuV3Entity.Id"/> (mỗi lệnh từ đúng một tín hiệu — V3-B4).</summary>
    public long TinHieuV3Id { get; set; }

    public string Symbol { get; set; } = string.Empty;
    public LoaiKichBanV3 Loai { get; set; }
    public DateOnly NgayTinHieu { get; set; }

    public TrangThaiViTheV3 TrangThai { get; set; }

    /// <summary>Phiên khớp mua (T mua — mốc tính T+2.5).</summary>
    public DateOnly? NgayVao { get; set; }
    /// <summary>Giá vào khớp (nghìn đồng).</summary>
    public decimal? GiaVao { get; set; }
    public decimal? KhoiLuong { get; set; }
    /// <summary>Giá trị lệnh (nghìn đồng).</summary>
    public decimal? GiaTriLenh { get; set; }

    /// <summary>Số phiên đã giữ tính từ phiên tín hiệu (FR-016).</summary>
    public int SoPhienDaGi { get; set; }

    /// <summary>
    /// Ngưỡng bán hiện hành — lệnh thường = max(TB n−1 giá đóng cửa trước, giá vào × (1+phí));
    /// lệnh hoảng loạn sâu (FR-015a) = giá vào × 1,15 (chốt cố định, nhịp 16:00 KHÔNG ghi đè).
    /// Nhịp mỗi phút chỉ so giá khớp với con số này, không nạp lịch sử (null = chưa có).
    /// </summary>
    public decimal? NguongChamMa { get; set; }

    /// <summary>
    /// Cờ lệnh hoảng loạn sâu (FR-015a/FR-016a): KB1/KB2/KB4/N1 có BREADTH50 phiên tín hiệu < 0,15.
    /// Chốt MỘT LẦN lúc tạo vị thế, không đổi về sau. Bật → bán ≥ giá vào × 1,15, giữ tối đa 40 phiên.
    /// </summary>
    public bool LaHoangLoanSau { get; set; }

    /// <summary>
    /// Giá tham chiếu của phiên hiện tại (đóng cửa phiên liền trước) — nhịp 16:00 ghi lại mỗi ngày,
    /// để nhịp mỗi phút kiểm tra giá trần (FR-014) / sàn trắng (FR-018) mà không nạp lịch sử.
    /// </summary>
    public decimal? GiaThamChieu { get; set; }

    /// <summary>Sàn giao dịch của mã (HOSE/HNX/UPCOM) — biên độ trần/sàn; null hiểu là HOSE.</summary>
    public string? Exchange { get; set; }

    public DateOnly? NgayThoat { get; set; }
    public decimal? GiaThoat { get; set; }
    /// <summary>Lý do thoát: ChamMA / ChotLoiHoangLoan / HetHanATC / AtoTheoLich.</summary>
    public string? LyDoThoat { get; set; }

    /// <summary>Lãi ròng % (đã trừ phí) khi đóng.</summary>
    public decimal? PhanTramLoiNhuan { get; set; }
    /// <summary>Xếp loại: "Thang"/"Thua"/"HoaVon".</summary>
    public string? KetQua { get; set; }

    public decimal? Mfe { get; set; }
    public decimal? Mae { get; set; }

    /// <summary>Đã bắn tin bán chưa (chống spam khi giá khớp rung nhiều phút).</summary>
    public bool DaBanTinThoat { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Chỉ báo thị trường ngày (Phụ lục G bảng <c>ChiSoNgayV3</c>) — tính một lần/ngày,
/// mọi kịch bản dùng chung, tránh tính lại mỗi phút.
/// </summary>
public class ChiSoNgayV3Entity
{
    public long Id { get; set; }

    public DateOnly Ngay { get; set; }
    public int SoMaUni { get; set; }
    public decimal Breadth50 { get; set; }
    public decimal ChiSo { get; set; }
    public decimal ChiSoMa50 { get; set; }
    public decimal ChiSoDoLechMa50PhanTram { get; set; }
    public int Left { get; set; }
    public int Tdm { get; set; }

    /// <summary>
    /// Danh sách mã thuộc UNI (TV20 ≥ ngưỡng, close ≥ ngưỡng) tính tại phiên <see cref="Ngay"/>,
    /// nối bằng dấu phẩy — chính là tập mã nhịp 08:30/14:25 phiên KẾ TIẾP phải nạp (~200 mã),
    /// và là tập tính lợi nhuận ngày của chỉ số phiên sau (research 01: UNI.shift()).
    /// </summary>
    public string? DanhSachUni { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
