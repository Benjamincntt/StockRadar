using StockRadar.Application.Abstractions;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.DTOs;

/// <summary>Kết quả một lần chạy Pha 1 (trước phiên) — trả về cho endpoint trigger thủ công.</summary>
public record Pha1KetQuaDto(
    int SoMaSoTuyen,
    int TongSoMaDauVao,
    int SoDangHinhThanh,
    int SoDangTheoDoi,
    IReadOnlyList<KetQuaKichBanDto> ChiTiet)
{
    /// <summary>Ánh xạ từ kết quả miền <see cref="Pha1KetQua"/> sang DTO.</summary>
    public static Pha1KetQuaDto From(Pha1KetQua ketQua) =>
        new(
            ketQua.SoTuyen.DanhSachMa.Count,
            ketQua.SoTuyen.TongSoMaDauVao,
            ketQua.SoDangHinhThanh,
            ketQua.SoDangTheoDoi,
            ketQua.KetQua.Select(KetQuaKichBanDto.From).ToList());
}

/// <summary>DTO một kết quả kịch bản cho một mã (kèm bằng chứng dạng chuỗi đọc được).</summary>
public record KetQuaKichBanDto(
    string Symbol,
    string LoaiKichBan,
    string TrangThai,
    decimal MucHoanThien,
    bool DatBoiCanh,
    bool DatHinhThai,
    IReadOnlyList<string> BangChung)
{
    /// <summary>Ánh xạ từ <see cref="KetQuaKichBan"/> (domain) sang DTO.</summary>
    public static KetQuaKichBanDto From(KetQuaKichBan k) =>
        new(
            k.Symbol,
            k.LoaiKichBan.ToString(),
            k.TrangThai.ToString(),
            k.MucHoanThien,
            k.DatBoiCanh,
            k.DatHinhThai,
            k.DanhSachBangChung.Select(DinhDangBangChung).ToList());

    /// <summary>Định dạng một bằng chứng thành chuỗi: "Mô tả: giá trị (ngưỡng) ✓/✗".</summary>
    private static string DinhDangBangChung(BangChung b) =>
        $"{b.MoTa}: {b.GiaTriThucTe} (ngưỡng {b.Nguong}) {(b.Dat ? "✓" : "✗")}";
}

/// <summary>DTO kết quả xếp hạng cơ hội V2 — một kịch bản TRIGGERED kèm điểm 6 tiêu chí.</summary>
public record XepHangDto(
    string Symbol,
    string LoaiKichBan,
    decimal DiemTong,
    decimal DiemRs,
    decimal DiemSector,
    decimal DiemTrigger,
    decimal DiemRegime,
    decimal DiemTyLeLaiLo,
    decimal DiemConfluence,
    KeHoachGiaoDichDto? KeHoach)
{
    /// <summary>Ánh xạ từ <see cref="KetQuaXepHang"/> (domain) sang DTO.</summary>
    public static XepHangDto From(KetQuaXepHang k) =>
        new(
            k.KetQua.Symbol,
            k.KetQua.LoaiKichBan.ToString(),
            k.DiemTong,
            k.DiemRs,
            k.DiemSector,
            k.DiemTrigger,
            k.DiemRegime,
            k.DiemTyLeLaiLo,
            k.DiemConfluence,
            KeHoachGiaoDichDto.From(k.KetQua.KeHoach));
}

/// <summary>Kết quả một lần chạy Pha 2 (trong phiên) — trả về cho endpoint trigger thủ công.</summary>
public record Pha2KetQuaDto(
    int SoTrigger,
    int SoAlert,
    IReadOnlyList<KetQuaKichBanDto> ChiTiet)
{
    /// <summary>Ánh xạ từ kết quả miền <see cref="Pha2KetQua"/> sang DTO.</summary>
    public static Pha2KetQuaDto From(Pha2KetQua ketQua) =>
        new(
            ketQua.SoTrigger,
            ketQua.SoAlert,
            ketQua.ChiTiet.Select(KetQuaKichBanDto.From).ToList());
}

/// <summary>Kết quả một lần chạy Pha 3 — đo lường outcome các kịch bản đã kích hoạt sau T+3 phiên.</summary>
public record Pha3KetQuaDto(
    int TongDoLuong,
    int Thang,
    int Thua,
    int Ngang,
    DateTime CompletedAt);

/// <summary>DTO kế hoạch giao dịch của một kịch bản đã trigger.</summary>
public record KeHoachGiaoDichDto(
    decimal GiaVaoLenhMin,
    decimal GiaVaoLenhMax,
    decimal GiaDungLo,
    decimal GiaChotLoi1,
    decimal GiaChotLoi2,
    decimal TyLeLaiLo,
    string DieuKienHuy)
{
    /// <summary>Ánh xạ từ <see cref="KeHoachGiaoDich"/> (domain) sang DTO; null nếu chưa có kế hoạch.</summary>
    public static KeHoachGiaoDichDto? From(KeHoachGiaoDich? k) =>
        k is null
            ? null
            : new(
                k.GiaVaoLenhMin,
                k.GiaVaoLenhMax,
                k.GiaDungLo,
                k.GiaChotLoi1,
                k.GiaChotLoi2,
                k.TyLeLaiLo,
                k.DieuKienHuy);
}
