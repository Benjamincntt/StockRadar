namespace StockRadar.Application.DTOs;

/// <summary>
/// Chi tiết kịch bản V2 của một mã cổ phiếu — bản ghi mới nhất (NgayDanhGia lớn nhất) cho mỗi loại kịch bản.
/// </summary>
/// <param name="Symbol">Mã cổ phiếu (theo chữ hoa lưu trong DB).</param>
/// <param name="DanhSachKichBan">Danh sách kịch bản (tối đa 5), sắp xếp theo loại kịch bản.</param>
public record KichBanTheoSymbolDto(
    string Symbol,
    IReadOnlyList<ChiTietKichBanDto> DanhSachKichBan);

/// <summary>
/// Chi tiết một kịch bản của mã — trạng thái vòng đời, 3 giai đoạn đánh giá (bối cảnh/hình thái/cò kích hoạt),
/// bằng chứng, kế hoạch giao dịch (khi đã kích hoạt) và kết quả đo Pha 3 (nếu có).
/// </summary>
/// <param name="LoaiKichBan">Tên enum loại kịch bản: NoHuongLen / HoiHoTro / QuetThanhKhoan / KietSuc / GayNen.</param>
/// <param name="TenKichBan">Tên hiển thị tiếng Việt ("Nổ hướng lên", "Hồi hỗ trợ", ...).</param>
/// <param name="TrangThai">Trạng thái vòng đời: DangTheoDoi / DangHinhThanh / DaKichHoat / DangGiu / ChotLoi / HuyLenh / ThoatLenh.</param>
/// <param name="MucHoanThien">Mức hoàn thiện kịch bản 0-100%.</param>
/// <param name="DatBoiCanh">Đạt giai đoạn bối cảnh không.</param>
/// <param name="DatHinhThai">Đạt giai đoạn hình thái không.</param>
/// <param name="DatCoKichHoat">Đạt giai đoạn cò kích hoạt không.</param>
/// <param name="NgayDanhGia">Ngày đánh giá (phiên giao dịch) của bản ghi mới nhất.</param>
/// <param name="ThoiGianKichHoat">Thời điểm kích hoạt (null nếu chưa kích hoạt).</param>
/// <param name="BangChung">Danh sách bằng chứng xác nhận điều kiện kịch bản (giải mã từ DanhSachBangChungJson).</param>
/// <param name="KeHoachGiaoDich">Kế hoạch giao dịch — chỉ có khi kịch bản đã kích hoạt trở lên (TrangThai &gt;= DaKichHoat).</param>
/// <param name="DiemXepHang">Điểm xếp hạng cơ hội 0-100 (null nếu chưa chấm điểm). Điểm thành phần không được lưu ở entity.</param>
/// <param name="KetQuaDoLuong">Kết quả đo Pha 3: "Thang" / "Thua" / "Ngang" (null nếu chờ đo).</param>
/// <param name="PhanTramLoiNhuan">Lợi nhuận thực tế (%) = (giá thoát − giá vào) / giá vào × 100.</param>
/// <param name="TyLeLaiLoThucTe">Tỷ lệ lãi/lỗ thực tế (R:R).</param>
/// <param name="GiaThoat">Giá thoát tại thời điểm đo kết quả.</param>
/// <param name="NgayThoat">Ngày đo kết quả (phiên lấy giá thoát).</param>
public record ChiTietKichBanDto(
    string LoaiKichBan,
    string TenKichBan,
    string TrangThai,
    decimal MucHoanThien,
    bool DatBoiCanh,
    bool DatHinhThai,
    bool DatCoKichHoat,
    DateOnly NgayDanhGia,
    DateTime? ThoiGianKichHoat,
    IReadOnlyList<BangChungKichBanDto> BangChung,
    KeHoachGiaoDichDto? KeHoachGiaoDich,
    decimal? DiemXepHang,
    string? KetQuaDoLuong,
    decimal? PhanTramLoiNhuan,
    decimal? TyLeLaiLoThucTe,
    decimal? GiaThoat,
    DateOnly? NgayThoat);

/// <summary>Một bằng chứng xác nhận điều kiện kịch bản.</summary>
/// <param name="VaiTro">Vai trò chỉ báo: BoiCanh / HinhThai / CoKichHoat / RuiRo.</param>
/// <param name="MoTa">Mô tả điều kiện (tiếng Việt).</param>
/// <param name="GiaTriThucTe">Giá trị thực tế đo được.</param>
/// <param name="Nguong">Ngưỡng yêu cầu.</param>
/// <param name="Dat">Điều kiện có đạt không.</param>
public record BangChungKichBanDto(
    string VaiTro,
    string MoTa,
    string GiaTriThucTe,
    string Nguong,
    bool Dat);
