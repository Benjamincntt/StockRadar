using StockRadar.Domain.Entities;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Phân loại một lần tải nến phút thành "đủ / thiếu / rỗng / lỗi" và dựng sẵn hai dòng cần ghi.
/// Luật phân loại và ngưỡng: specs/017-intraday-minute-bars/plan.md (D3).
/// </summary>
internal static class NenPhutChatLuong
{
    public const string Du = "Du";
    public const string Thieu = "Thieu";
    public const string Rong = "Rong";
    public const string Loi = "Loi";

    /// <summary>
    /// Phân loại kết quả tải và dựng dòng nến (nếu có nến) cùng dòng sổ chất lượng.
    /// </summary>
    /// <param name="symbol">Mã cổ phiếu.</param>
    /// <param name="ngayPhien">Phiên của khối nến.</param>
    /// <param name="ketQua">Kết quả KBS kèm trạng thái.</param>
    /// <param name="nguongThieuNen">Số nến tối thiểu của một phiên đủ (Q5: 200).</param>
    /// <param name="soLanThu">Số lần đã thử trong lần chạy này.</param>
    /// <param name="lanGhi">Nguồn ghi: <c>Job</c>, <c>NapMotLan</c> hoặc <c>ChayBu</c>.</param>
    /// <returns>Dòng nến (null khi không có nến để lưu) và dòng sổ chất lượng tương ứng.</returns>
    public static (NenPhutTheoPhienEntity? Nen, SoChatLuongNenPhutEntity ChatLuong) PhanLoai(
        string symbol,
        DateOnly ngayPhien,
        KbsKetQuaTai ketQua,
        int nguongThieuNen,
        int soLanThu,
        string lanGhi)
    {
        ArgumentNullException.ThrowIfNull(ketQua);

        var ma = symbol.Trim().ToUpperInvariant();
        var coNen = ketQua.ThanhCong && ketQua.Bars.Count > 0;
        var nenAtc = coNen ? NenPhutCodec.TimNenAtc(ketQua.Bars) : null;

        var trangThai = ketQua.TrangThai switch
        {
            KbsTrangThaiTai.Ok when ketQua.Bars.Count >= nguongThieuNen => Du,
            KbsTrangThaiTai.Ok => Thieu,
            KbsTrangThaiTai.Rong => Rong,
            _ => Loi,
        };

        var dongNen = coNen
            ? new NenPhutTheoPhienEntity
            {
                Symbol = ma,
                NgayPhien = ngayPhien,
                SoNen = ketQua.Bars.Count,
                Nguon = "KBS",
                DuLieuNen = NenPhutCodec.Nen(ketQua.Bars),
                ThoiDiemGhi = DateTime.UtcNow,
            }
            : null;

        var dongChatLuong = new SoChatLuongNenPhutEntity
        {
            Symbol = ma,
            NgayPhien = ngayPhien,
            SoNenNhanDuoc = ketQua.Bars.Count,
            TrangThai = trangThai,
            GioNenAtc = nenAtc is null ? null : TimeOnly.FromDateTime(nenAtc.Time.DateTime),
            SoLanThu = soLanThu,
            ThongDiepLoi = ketQua.Loi,
            LanGhi = lanGhi,
            ThoiDiemGhi = DateTime.UtcNow,
        };

        return (dongNen, dongChatLuong);
    }
}
