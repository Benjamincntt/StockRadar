namespace StockRadar.Application.DTOs;

/// <summary>Request thêm/sửa lệnh nhật ký.UserId KHÔNG nằm trong body — luôn lấy từ JWT (N6).</summary>
public sealed record TaoLenhRequest(
    string Symbol,
    string LoaiLenh,
    DateTime ThoiDiemKhop,
    decimal? GiaKhop,
    decimal? KhoiLuong,
    decimal? Phi,
    string? NguonTinHieu,
    string? MaTinHieu,
    string? LyDo,
    string? GhiChu,
    decimal? KLieuChinh,
    decimal? VonDieuChinh);

public sealed record LenhDto(
    Guid Id,
    string Symbol,
    string LoaiLenh,
    DateTime ThoiDiemKhop,
    decimal GiaKhop,
    decimal KhoiLuong,
    decimal? Phi,
    string? NguonTinHieu,
    string? MaTinHieu,
    decimal? GiaTinHieuChup,
    decimal? LechSoTinHieu,
    decimal? LechSoTinHieuPhanTram,
    string? LyDo,
    string? GhiChu,
    decimal? KLieuChinh,
    decimal? VonDieuChinh,
    DateTime CreatedAt,
    string? KetQuaTinHieu = null,
    decimal? KetQuaTinHieuPhanTram = null);

public sealed record NhatKyDanhSachResponse(
    IReadOnlyList<LenhDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>Vị thế theo mã — projection on-the-fly từ các lệnh (FR-004), không lưu bảng riêng. US3: gồm cả vị thế đã đóng + lãi/lỗ ròng.</summary>
public sealed record ViTheDto(
    string Symbol,
    decimal KhoiLuongMo,
    decimal GiaVonBinhQuan,
    decimal TienVonDangKe,
    DateOnly NgayMo,
    int SoLenhDaBan,
    string? NguonTinHieuGan,
    string? MaTinHieuGan,
    bool DaDong = false,
    DateOnly? NgayDong = null,
    decimal? ReturnOnDeployedPercent = null,
    decimal? LoiNhuanRongVND = null,
    IReadOnlyList<NhipBanDto>? NhipBan = null);

/// <summary>US3: một nhịp bán đã đóng — lãi ròng % theo RealizedPnlMath trên giá vốn BQ tại nhịp.</summary>
public sealed record NhipBanDto(
    DateOnly Ngay,
    decimal GiaBan,
    decimal KhoiLuong,
    decimal GiaVonTaiNhip,
    decimal LaiRongPhanTram);

/// <summary>US3 — bộ đếm vị thế đã đóng theo ngưỡng thắng (FR-006); tỷ lệ thắng loại vị thế đang mở.</summary>
public sealed record ThongKeViTheDto(int Tang, int Thua, int Ngang, int ChoDo, decimal? TyLeThang);

/// <summary>US3 — bucket theo tháng (lấy ngày nhịp bán cuối của vị thế đã đóng).</summary>
public sealed record ThongKeTheoKyDto(DateOnly KyBatDau, int Thang, int Thua, decimal TongLoiNhuanRongVND);

/// <summary>US3 — tổng hợp tài khoản thật theo kỳ (FR-006). NguongThangPhanTram nêu rõ ngưỡng "thắng = lãi ròng > ngưỡng".</summary>
public sealed record ThongKeDto(
    ThongKeViTheDto ViThe,
    int SoLenh,
    decimal TongLoiNhuanRongVND,
    decimal TongDoLosuDungVND,
    decimal NguongThangPhanTram,
    decimal? TyLeKhopTinHieu,
    int TinHieuCoLenh,
    int TinHieuBoQua,
    decimal? LechTBSoTinHieuPhanTram,
    decimal? LechTBMuaPhanTram,
    decimal? LechTBBanPhanTram,
    IReadOnlyList<ThongKeTheoKyDto> TheoKy);

/// <summary>Kế hoạch trích từ KeHoachGiaoDichJson — form mobile điền sẵn (N1); kèm giá các nhịp đã khớp.</summary>
public sealed record KeHoachPickerDto(
    decimal? GiaVaoMin,
    decimal? GiaVaoMax,
    decimal? DungLo,
    decimal? ChotLoi1,
    decimal? ChotLoi2,
    decimal? GiaMuaDiem2,
    decimal? GiaMuaDiem2ThucTe,
    decimal? GiaBanNua,
    decimal? GiaThoatHet);

public sealed record TinHieuPickerItem(
    string Nguon,
    string MaTinHieu,
    string Symbol,
    string? LoaiKichBan,
    DateOnly Ngay,
    decimal? GiaTinHieu,
    KeHoachPickerDto? KeHoach,
    string? KetQuaTinHieu,
    decimal? KetQuaTinHieuPhanTram,
    bool DaCoLenh,
    bool DaBoQua);

public sealed record TinHieuPickerResponse(
    IReadOnlyList<TinHieuPickerItem> Items,
    int TotalCount,
    int Page,
    int PageSize);

/// <summary>Body PUT bo-qua — toàn bộ field tuỳ chọn.</summary>
public sealed record BoQuaRequest(string? LyDoBoQua);
