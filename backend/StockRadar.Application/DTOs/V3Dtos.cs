using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;

namespace StockRadar.Application.DTOs;

/// <summary>Kết quả một lần chạy job V3 (để log/endpoint thủ công báo về).</summary>
public sealed record V3ChayKetQua(int SoTinHieu, int SoLenhMo, int SoLenhThoat);

/// <summary>Kết quả lần nạp một lần khi deploy: chuỗi chỉ số + UNI theo ngày vào ChiSoNgayV3.</summary>
public sealed record V3NapChiSoKetQua(
    int SoPhienDaLuu,
    DateOnly TuNgay,
    DateOnly DenNgay,
    int SoMaUniPhienCuoi);

/// <summary>Tóm tắt hiệu quả một kịch bản V3 (Phụ lục H khối 1).</summary>
public sealed record HieuQuaTheoKichBanV3Dto(
    LoaiKichBanV3 Loai,
    string Ten,
    int SoLenhDong,
    int SoThang,
    int SoThua,
    int SoHoaVon,
    decimal TyLeThangPhanTram,
    decimal LaiTBPhanTram,
    decimal LoTBPhanTram,
    decimal TongLaiTongLo,
    decimal TeNhatPhanTram,
    decimal GiuTBPhien);

/// <summary>Tóm tắt danh mục V3 (Phụ lục H khối 2).</summary>
public sealed record HieuQuaDanhMucV3Dto(
    decimal GiaTriHienTai,
    decimal TienMat,
    int SoViTheDangGiu,
    decimal SutGiamToiDaPhanTram);

/// <summary>TOÀTAT hiệu quả V3 cho màn "Hiệu quả".</summary>
public sealed record HieuQuaTomTatV3Dto(
    IReadOnlyList<HieuQuaTheoKichBanV3Dto> TheoKichBan,
    HieuQuaDanhMucV3Dto? DanhMuc,
    DateOnly? DenNgay);

/// <summary>Một vị thế / lệnh V3 (Phụ lục H khối 3 + picker/stock detail).</summary>
public sealed record ViTheV3Dto(
    long Id,
    string Symbol,
    LoaiKichBanV3 Loai,
    DateOnly NgayTinHieu,
    DateOnly? NgayVao,
    decimal? GiaVao,
    decimal? GiaTriLenh,
    TrangThaiViTheV3 TrangThai,
    int SoPhienDaGi,
    DateOnly? NgayThoat,
    decimal? GiaThoat,
    string? LyDoThoat,
    decimal? PhanTramLoiNhuan,
    string? KetQua,
    /// <summary>Cờ lệnh hoảng loạn sâu (FR-015a/FR-024) — bán ≥ giá vào × 1,15, giữ tối đa 40 phiên.</summary>
    bool LaHoangLoanSau = false);

/// <summary>Một tín hiệu V3 kèm snapshot chỉ báo (màn chi tiết mã).</summary>
public sealed record TinHieuV3Dto(
    long Id,
    string Symbol,
    LoaiKichBanV3 Loai,
    DateOnly NgayTinHieu,
    decimal GiaTinHieu,
    int Hang,
    decimal GiaTriXepHang,
    bool MuaMoCuaPhienSau,
    TrangThaiTinHieuV3 TrangThai,
    string? SnapshotJson,
    decimal? GiaVao,
    decimal? GiaThoat,
    string? KetQua,
    bool LaHoangLoanSau = false);

/// <summary>Phân trang vị thế V3.</summary>
public sealed record ViTheV3PagedDto(
    IReadOnlyList<ViTheV3Dto> Items,
    int Total,
    int Page,
    int Size);
