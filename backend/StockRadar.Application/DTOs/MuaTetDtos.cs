namespace StockRadar.Application.DTOs;

/// <summary>Một mã trong giỏ "Mùa Tết — mã khỏe".</summary>
/// <param name="GiaDongCua">Giá đóng cửa phiên dữ liệu, nghìn đồng (cùng đơn vị DB).</param>
/// <param name="GanDinh">Đóng cửa ÷ giá cao nhất 250 phiên (0–1).</param>
/// <param name="GiaTriGiaoDichTb20Ty">Giá trị giao dịch TB 20 phiên, tỷ đồng.</param>
/// <param name="GiaMotLo">1 lô (100 cổ) × giá đóng cửa, đơn vị đồng.</param>
/// <param name="SoLoGoiY">Số lô gợi ý cho một chỗ vốn, tối thiểu 1.</param>
public sealed record MuaTetMaDto(
    int ThuTu,
    string Symbol,
    string Sector,
    decimal GiaDongCua,
    decimal GanDinh,
    decimal GiaTriGiaoDichTb20Ty,
    decimal GiaMotLo,
    int SoLoGoiY);

/// <summary>Giỏ "Mùa Tết — mã khỏe" (P4-19) của một ngày dữ liệu.</summary>
/// <param name="NgayDuLieu">Ngày của nến hoàn thành gần nhất được dùng.</param>
/// <param name="VonBanDau">Vốn cả giỏ giả định, đơn vị đồng.</param>
/// <param name="GiaTriMotCho">Vốn ÷ số mã của giỏ, đơn vị đồng.</param>
/// <param name="SoMa">Số mã tối đa của giỏ theo cấu hình.</param>
/// <param name="ToiDaMoiNganh">Số mã tối đa một ngành theo cấu hình.</param>
/// <param name="BiLoaiLanDonVi">Mã bị loại vì giá lưu lẫn đơn vị (nghìn đồng → đồng).</param>
/// <param name="BiBoVuotCho">Mã đủ điểm nhưng bị bỏ vì 1 lô vượt 1,5 lần giá trị một chỗ.</param>
public sealed record MuaTetGioDto(
    string Ten,
    DateOnly NgayDuLieu,
    decimal VonBanDau,
    decimal GiaTriMotCho,
    IReadOnlyList<MuaTetMaDto> CacMa,
    IReadOnlyList<string> BiLoaiLanDonVi,
    IReadOnlyList<string> BiBoVuotCho,
    int SoMa = 10,
    int ToiDaMoiNganh = 2);

/// <summary>Một mã trong giỏ Mùa Tết ĐÃ MUA, kèm giá hiện tại và lãi lỗ.</summary>
/// <param name="GiaVao">Giá vào do owner ghi trong cấu hình, nghìn đồng.</param>
/// <param name="GiaHienTai">Giá dùng để tính (thiếu giá thì chính là giá vào), nghìn đồng.</param>
/// <param name="ThieuGia">Không có giá hiện tại (ngừng giao dịch / LastClose ≤ 0).</param>
/// <param name="Von">Giá vào × số cổ × 1000, đồng.</param>
/// <param name="GiaTriHienTai">Giá hiện tại × số cổ × 1000, đồng.</param>
/// <param name="LaiLoPhanTram">(giá hiện tại ÷ giá vào − 1) × 100.</param>
/// <param name="LaiLoDong">Giá trị hiện tại − vốn, đồng.</param>
/// <param name="GiaThamChieu">Giá đóng cửa phiên trước, nghìn đồng; null khi thiếu giá hoặc thiếu nến phiên trước.</param>
/// <param name="ThayDoiTrongNgayPhanTram">(giá hiện tại ÷ giá tham chiếu − 1) × 100; null như trên.</param>
/// <param name="LaiLoTrongNgayDong">(giá hiện tại − giá tham chiếu) × số cổ × 1000, đồng; null như trên.</param>
public sealed record MuaTetTheoDoiMaDto(
    string Symbol,
    decimal GiaVao,
    int SoCo,
    decimal GiaHienTai,
    bool ThieuGia,
    decimal Von,
    decimal GiaTriHienTai,
    decimal LaiLoPhanTram,
    decimal LaiLoDong = 0m,
    decimal? GiaThamChieu = null,
    decimal? ThayDoiTrongNgayPhanTram = null,
    decimal? LaiLoTrongNgayDong = null);

/// <summary>Một mã của giỏ Mùa Tết đọc từ Nhật ký lệnh: giá vốn bình quân (nghìn đồng, gồm phí mua), khối lượng còn giữ, lúc khớp lệnh Mua MUATET đầu tiên (UTC).</summary>
public sealed record MuaTetMaNhatKy(string Symbol, decimal GiaVonBinhQuan, decimal KhoiLuongMo, DateTime MuaDauUtc);

/// <summary>Trạng thái giỏ Mùa Tết đã mua tại thời điểm kiểm.</summary>
/// <param name="TyLe">Giá trị giỏ ÷ vốn đã mua (0,95 = mất 5%).</param>
/// <param name="Muc">BinhThuong / CanhBao (≤ 95%) / Ban (≤ 90%).</param>
/// <param name="MaKeoXuongNhieuNhat">Mã lỗ nhiều nhất; null khi không mã nào lỗ.</param>
/// <param name="DaGuiTin">Lần kiểm này có gửi tin không (bị chặn lặp trong ngày thì false).</param>
/// <param name="ThongDiep">Lý do không theo dõi / không gửi, để owner đọc log.</param>
/// <param name="LaiLoDong">Giá trị giỏ − vốn, đồng.</param>
/// <param name="NgayPhien">Phiên của giá hiện tại (ngày nến mới nhất đã lưu); null khi giỏ trống.</param>
/// <param name="LaiLoTrongNgayDong">Tổng lãi/lỗ trong ngày của các mã có giá tham chiếu; null khi không mã nào có.</param>
/// <param name="ThayDoiTrongNgayPhanTram">Lãi/lỗ trong ngày ÷ giá trị giỏ theo giá tham chiếu × 100; null như trên.</param>
/// <param name="MaMua">Mã mùa dùng cho lệnh Nhật ký nguồn MUATET (ví dụ 2026-27).</param>
/// <param name="NguonGio">NhatKy = giỏ đọc từ Nhật ký lệnh của tài khoản cấu hình; CauHinh = giỏ ghi tay trong GioDaMua.</param>
public sealed record MuaTetTheoDoiDto(
    bool TheoDoiBat,
    DateOnly? NgayMua,
    decimal VonDaMua,
    decimal GiaTriHienTai,
    decimal TyLe,
    string Muc,
    IReadOnlyList<MuaTetTheoDoiMaDto> CacMa,
    string? MaKeoXuongNhieuNhat,
    int SoMaThieuGia,
    bool DaGuiTin,
    string? ThongDiep,
    decimal LaiLoDong = 0m,
    DateOnly? NgayPhien = null,
    decimal? LaiLoTrongNgayDong = null,
    decimal? ThayDoiTrongNgayPhanTram = null,
    string? MaMua = null,
    string NguonGio = "CauHinh");
