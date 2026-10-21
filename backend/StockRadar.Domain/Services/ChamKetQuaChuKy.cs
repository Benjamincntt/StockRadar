using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

/// <summary>
/// Công thức chấm DUY NHẤT cho một chu kỳ đã đóng (C-27/C-33): Pha 2 chấm ngay lượt bán hết,
/// Pha 3 chấm bù chu kỳ lỡ. Không được nhân bản công thức ở runner — hai bản lệch nhau là hai kết quả khác nhau.
/// </summary>
public static class ChamKetQuaChuKy
{
    public const string Thang = "Thang";
    public const string Thua = "Thua";
    public const string Ngang = "Ngang";

    /// <summary>Kết quả dòng "Huy" — chu kỳ bị huỷ vĩnh viễn không chấm và không bị ghi đè.</summary>
    public const string Huy = "Huy";

    /// <summary>Ngưỡng % lợi nhuận để phân loại Thắng/Thua (±1%).</summary>
    public const decimal NguongPhanTram = 1m;

    /// <summary>Kết quả chấm một chu kỳ, sẵn sàng ghi lên dòng KetQuaKichBan.</summary>
    public readonly record struct KetQua(
        string Loai,
        decimal GiaVao,
        decimal GiaThoat,
        decimal PhanTram,
        decimal? TyLeLaiLo,
        TrangThaiKichBan? TrangThaiMoi,
        DateOnly NgayThoat);

    /// <summary>
    /// Chấm từ các trường khớp thực tế của chu kỳ. Thiếu dữ liệu bắt buộc
    /// (giá bán hết rỗng hoặc giá vào &lt;= 0) → trả null — caller bỏ qua, không tự bịa giá.
    /// </summary>
    /// <param name="keHoach">Kế hoạch giao dịch (nguồn biên dưới vùng vào và SL tính R:R).</param>
    /// <param name="giaVaoThucTe">Giá khớp điểm 1, null với dòng cũ → dùng biên dưới vùng vào.</param>
    /// <param name="giaMuaDiem2ThucTe">Giá khớp điểm 2 (có thì giá vào là bình quân hai lần mua).</param>
    /// <param name="giaBanNua">Giá bán nửa, null nếu chu kỳ chưa bán nửa.</param>
    /// <param name="giaThoatHet">Giá bán hết — bắt buộc phải có khi chu kỳ đã đóng.</param>
    /// <param name="ngayThoat">Ngày thoát theo lịch Việt Nam của ThoiGianThoatHet.</param>
    public static KetQua? Tinh(
        KeHoachGiaoDich keHoach,
        decimal? giaVaoThucTe,
        decimal? giaMuaDiem2ThucTe,
        decimal? giaBanNua,
        decimal? giaThoatHet,
        DateOnly ngayThoat)
    {
        // BUSINESS-RULE: C-4/Q6 — giá vào = giá khớp điểm 1 (thiếu thì biên dưới vùng vào), có điểm 2 thì bình quân hai lần mua.
        var giaVaoDiem1 = giaVaoThucTe ?? keHoach.GiaVaoLenhMin;
        var giaVao = giaMuaDiem2ThucTe is { } giaDiem2
            ? (giaVaoDiem1 + giaDiem2) / 2m
            : giaVaoDiem1;

        if (giaThoatHet is null || giaVao <= 0)
            return null;

        // BUSINESS-RULE: C-27 — giá ra = bình quân (bán nửa + bán hết), chưa bán nửa thì dùng thẳng giá bán hết.
        var giaThoat = giaBanNua is { } giaNua
            ? (giaNua + giaThoatHet.Value) / 2m
            : giaThoatHet.Value;

        var pTram = (giaThoat - giaVao) / giaVao * 100m;
        string loai;
        TrangThaiKichBan? trangThaiMoi;
        if (pTram >= NguongPhanTram)
        {
            loai = Thang;
            trangThaiMoi = TrangThaiKichBan.ChotLoi;
        }
        else if (pTram <= -NguongPhanTram)
        {
            loai = Thua;
            trangThaiMoi = TrangThaiKichBan.HuyLenh;
        }
        else
        {
            loai = Ngang;
            trangThaiMoi = null;
        }

        // R:R thực tế = (giá ra − giá vào) / (giá vào − SL kế hoạch); giá vào trùng SL thì bỏ qua.
        decimal? tyLeLaiLo = null;
        var ruiRo = giaVao - keHoach.GiaDungLo;
        if (ruiRo != 0)
            tyLeLaiLo = Math.Round((giaThoat - giaVao) / ruiRo, 4);

        return new KetQua(loai, giaVao, giaThoat, Math.Round(pTram, 4), tyLeLaiLo, trangThaiMoi, ngayThoat);
    }

    /// <summary>Ghi kết quả chấm lên dòng chu kỳ. Không đụng UpdatedAt — caller tự quyết khi lưu.</summary>
    public static void GhiVaoDong(KetQuaKichBanEntity entity, KetQua ketQua)
    {
        entity.PhanTramLoiNhuan = ketQua.PhanTram;
        entity.GiaThoat = ketQua.GiaThoat;
        entity.NgayThoat = ketQua.NgayThoat;
        entity.TyLeLaiLoThucTe = ketQua.TyLeLaiLo;
        entity.KetQuaDoLuong = ketQua.Loai;
        if (ketQua.TrangThaiMoi is { } trangThai)
            entity.TrangThai = trangThai;
    }
}
