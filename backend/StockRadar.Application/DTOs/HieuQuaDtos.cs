namespace StockRadar.Application.DTOs;

/// <summary>
/// Tóm tắt hiệu quả tổng hợp theo kỳ (week/month/quarter/all) của các kịch bản đã kích hoạt.
/// </summary>
/// <param name="TongKichHoat">Tổng số kịch bản đã kích hoạt trong kỳ.</param>
/// <param name="Thang">Số lệnh đo ra "Thắng".</param>
/// <param name="Thua">Số lệnh đo ra "Thua".</param>
/// <param name="Ngang">Số lệnh đo ra "Ngang".</param>
/// <param name="ChoDo">Số lệnh đã kích hoạt nhưng chưa đến hạn đo (KetQuaDoLuong = null).</param>
/// <param name="TyLeThang">Tỷ lệ thắng (%) = Thang / (Thang + Thua + Ngang) × 100 — loại "Chờ đo" khỏi mẫu.</param>
/// <param name="TbRR">Trung bình R:R thực tế (chỉ tính các lệnh đã đo).</param>
/// <param name="TbPhanTram">Trung bình % lợi nhuận thực tế (chỉ tính các lệnh đã đo).</param>
/// <param name="TheoLoaiKichBan">Thống kê chi tiết theo từng loại kịch bản.</param>
public record HieuQuaTomTatDto(
    int TongKichHoat,
    int Thang,
    int Thua,
    int Ngang,
    int ChoDo,
    decimal TyLeThang,
    decimal TbRR,
    decimal TbPhanTram,
    IReadOnlyList<LoaiKichBanStatsDto> TheoLoaiKichBan);

/// <summary>
/// Thống kê hiệu quả theo từng loại kịch bản.
/// </summary>
/// <param name="TenKichBan">Tên hiển thị tiếng Việt của loại kịch bản.</param>
public record LoaiKichBanStatsDto(
    string TenKichBan,
    int Tong,
    int Thang,
    int Thua,
    int Ngang,
    decimal TyLeThang,
    decimal TbRR);

/// <summary>
/// Một dòng trong lịch sử lệnh (kịch bản đã kích hoạt).
/// </summary>
/// <param name="LoaiKichBan">Tên hiển thị tiếng Việt của loại kịch bản.</param>
/// <param name="KetQua">"Thắng" / "Thua" / "Ngang" / "Chờ đo".</param>
/// <param name="GiaVao">Giá vào lệnh (lấy từ KeHoachGiaoDichJson.GiaVaoLenhMin).</param>
public record LichSuLenhDto(
    int Id,
    string Symbol,
    string LoaiKichBan,
    string KetQua,
    decimal GiaVao,
    decimal? GiaThoat,
    decimal? PhanTram,
    decimal? RRThucTe,
    DateOnly NgayKichHoat,
    DateOnly? NgayThoat);

/// <summary>
/// Chi tiết một lệnh kèm snapshot dữ liệu thô lúc kích hoạt (UI tự phân tích/hiển thị).
/// </summary>
/// <param name="Lenh">Thông tin tóm tắt của lệnh.</param>
/// <param name="BangChungJson">Danh sách bằng chứng tại thời điểm kích hoạt (JSON thô).</param>
/// <param name="BangChupChiBaoJson">Bản chụp đầy đủ 24 chỉ báo tại thời điểm kích hoạt (JSON thô).</param>
/// <param name="KeHoachJson">Kế hoạch giao dịch — vùng vào/dừng lỗ/chốt lời (JSON thô).</param>
public record ChiTietLenhDto(
    LichSuLenhDto Lenh,
    string? BangChungJson,
    string? BangChupChiBaoJson,
    string? KeHoachJson);
