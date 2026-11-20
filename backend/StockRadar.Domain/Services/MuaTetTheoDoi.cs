namespace StockRadar.Domain.Services;

/// <summary>
/// Kịch bản "Mùa Tết — mã khỏe" (P4-19, sổ V4) — theo dõi giỏ ĐÃ MUA: tính vốn, giá trị hiện tại,
/// tỷ lệ so vốn và mức báo (bình thường / cảnh báo sớm / bán cả giỏ).
/// Hàm THUẦN: đầu vào là giỏ đã mua + giá hiện tại theo mã; không gọi DB, không gọi mạng.
/// </summary>
public static class MuaTetTheoDoi
{
    /// <summary>Ngưỡng cảnh báo sớm mặc định: giá trị giỏ ≤ 95% vốn.</summary>
    public const decimal NguongCanhBaoMacDinh = 0.95m;

    /// <summary>Ngưỡng bán cả giỏ mặc định (P4-19): giá trị giỏ ≤ 90% vốn.</summary>
    public const decimal NguongBanMacDinh = 0.90m;

    /// <summary>Mức báo của một lần kiểm giỏ.</summary>
    public enum MucBao
    {
        /// <summary>Trên ngưỡng cảnh báo — không gửi tin.</summary>
        BinhThuong = 0,

        /// <summary>≤ ngưỡng cảnh báo (95%) nhưng chưa tới ngưỡng bán.</summary>
        CanhBao = 1,

        /// <summary>≤ ngưỡng bán (90%) — bán cả giỏ.</summary>
        Ban = 2
    }

    /// <summary>Một mã đã mua: mã, giá vào (nghìn đồng, cùng đơn vị DB) và số cổ.</summary>
    public sealed record MaDaMua(string Symbol, decimal GiaVao, int SoCo);

    /// <summary>Một dòng trong giỏ đã mua, đủ số liệu để in tin.</summary>
    /// <param name="GiaHienTai">Giá dùng để tính — khi thiếu giá thì chính là giá vào.</param>
    /// <param name="ThieuGia">Không có giá hiện tại (ngừng giao dịch / LastClose ≤ 0) nên đã dùng giá vào.</param>
    /// <param name="LaiLoPhanTram">(giá hiện tại ÷ giá vào − 1) × 100.</param>
    /// <param name="LaiLoDong">Giá trị hiện tại − vốn, đồng.</param>
    /// <param name="GiaThamChieu">Giá đóng cửa phiên trước, nghìn đồng; null khi thiếu giá hoặc thiếu tham chiếu.</param>
    /// <param name="ThayDoiTrongNgayPhanTram">(giá hiện tại ÷ giá tham chiếu − 1) × 100; null như trên.</param>
    /// <param name="LaiLoTrongNgayDong">(giá hiện tại − giá tham chiếu) × số cổ × 1000, đồng; null như trên.</param>
    public sealed record ChiTietMa(
        string Symbol,
        decimal GiaVao,
        int SoCo,
        decimal GiaHienTai,
        bool ThieuGia,
        decimal VonDong,
        decimal GiaTriDong,
        decimal LaiLoPhanTram,
        decimal LaiLoDong,
        decimal? GiaThamChieu,
        decimal? ThayDoiTrongNgayPhanTram,
        decimal? LaiLoTrongNgayDong);

    /// <summary>Kết quả một lần kiểm giỏ.</summary>
    /// <param name="TyLe">Giá trị giỏ ÷ vốn đã mua (1 = hoà vốn, 0 khi giỏ rỗng).</param>
    /// <param name="MaKeoXuongNhieuNhat">Mã lỗ nhiều nhất (đồng); null khi không mã nào lỗ.</param>
    /// <param name="SoMaThieuGia">Số mã phải dùng giá vào vì thiếu giá hiện tại.</param>
    /// <param name="LaiLoDong">Giá trị giỏ − vốn, đồng.</param>
    /// <param name="LaiLoTrongNgayDong">Tổng lãi/lỗ trong ngày của các mã có giá tham chiếu; null khi không mã nào có.</param>
    /// <param name="ThayDoiTrongNgayPhanTram">Lãi/lỗ trong ngày ÷ giá trị theo giá tham chiếu của các mã đó × 100; null như trên.</param>
    public sealed record KetQuaTheoDoi(
        decimal VonDong,
        decimal GiaTriDong,
        decimal TyLe,
        MucBao Muc,
        IReadOnlyList<ChiTietMa> ChiTiet,
        string? MaKeoXuongNhieuNhat,
        int SoMaThieuGia,
        decimal LaiLoDong = 0m,
        decimal? LaiLoTrongNgayDong = null,
        decimal? ThayDoiTrongNgayPhanTram = null)
    {
        /// <summary>Có giỏ để theo dõi (giỏ cấu hình còn mã hợp lệ).</summary>
        public bool CoGio => ChiTiet.Count > 0;
    }

    /// <summary>
    /// Tính giá trị giỏ và mức báo. Giá thiếu (không có trong từ điển hoặc ≤ 0) thì dùng giá vào kèm cờ
    /// <see cref="ChiTietMa.ThieuGia"/> — không để một mã mất giá làm cả giỏ rơi về 0.
    /// </summary>
    /// <param name="gioDaMua">Giỏ đã mua; mã có giá vào ≤ 0 hoặc số cổ ≤ 0 bị bỏ qua.</param>
    /// <param name="giaHienTai">Giá hiện tại theo mã, nghìn đồng; null/thiếu = dùng giá vào.</param>
    /// <param name="nguongCanhBao">Ngưỡng cảnh báo sớm (0,95).</param>
    /// <param name="nguongBan">Ngưỡng bán cả giỏ (0,90).</param>
    /// <param name="giaThamChieu">Giá đóng cửa phiên trước theo mã, nghìn đồng; null/thiếu = không tính số trong ngày cho mã đó.</param>
    /// <returns>Kết quả kèm chi tiết từng mã; giỏ rỗng trả <see cref="MucBao.BinhThuong"/>.</returns>
    public static KetQuaTheoDoi Tinh(
        IReadOnlyList<MaDaMua> gioDaMua,
        IReadOnlyDictionary<string, decimal>? giaHienTai,
        decimal nguongCanhBao = NguongCanhBaoMacDinh,
        decimal nguongBan = NguongBanMacDinh,
        IReadOnlyDictionary<string, decimal>? giaThamChieu = null)
    {
        var chiTiet = new List<ChiTietMa>(gioDaMua.Count);
        decimal von = 0m;
        decimal giaTri = 0m;
        var thieuGia = 0;
        var loNhat = 0m;
        string? maLoNhat = null;
        decimal laiLoTrongNgay = 0m;
        decimal giaTriThamChieu = 0m;
        var coThamChieu = false;

        foreach (var ma in gioDaMua)
        {
            if (string.IsNullOrWhiteSpace(ma.Symbol) || ma.GiaVao <= 0m || ma.SoCo <= 0)
                continue;

            var symbol = ma.Symbol.Trim().ToUpperInvariant();
            var coGia = false;
            var giaHienTaiDung = ma.GiaVao;
            if (giaHienTai is not null
                && giaHienTai.TryGetValue(symbol, out var gia)
                && gia > 0m)
            {
                coGia = true;
                giaHienTaiDung = gia;
            }
            else
            {
                thieuGia++;
            }

            var vonDong = ma.GiaVao * 1000m * ma.SoCo;
            var giaTriDong = giaHienTaiDung * 1000m * ma.SoCo;
            von += vonDong;
            giaTri += giaTriDong;

            var chenh = giaTriDong - vonDong;
            if (chenh < loNhat)
            {
                loNhat = chenh;
                maLoNhat = symbol;
            }

            // Thiếu giá hiện tại thì giá đang dùng là giá vào — so với tham chiếu sẽ ra số trong ngày giả, nên bỏ.
            decimal? thamChieu = null;
            decimal? thayDoiTrongNgay = null;
            decimal? laiLoTrongNgayMa = null;
            if (coGia
                && giaThamChieu is not null
                && giaThamChieu.TryGetValue(symbol, out var tc)
                && tc > 0m)
            {
                thamChieu = tc;
                thayDoiTrongNgay = Math.Round((giaHienTaiDung / tc - 1m) * 100m, 2);
                var chenhNgay = (giaHienTaiDung - tc) * 1000m * ma.SoCo;
                laiLoTrongNgayMa = Math.Round(chenhNgay, 0);
                laiLoTrongNgay += chenhNgay;
                giaTriThamChieu += tc * 1000m * ma.SoCo;
                coThamChieu = true;
            }

            chiTiet.Add(new ChiTietMa(
                symbol,
                ma.GiaVao,
                ma.SoCo,
                giaHienTaiDung,
                !coGia,
                Math.Round(vonDong, 0),
                Math.Round(giaTriDong, 0),
                Math.Round((giaHienTaiDung / ma.GiaVao - 1m) * 100m, 2),
                Math.Round(chenh, 0),
                thamChieu,
                thayDoiTrongNgay,
                laiLoTrongNgayMa));
        }

        if (von <= 0m)
            return new KetQuaTheoDoi(0m, 0m, 0m, MucBao.BinhThuong, chiTiet, null, thieuGia);

        decimal? laiLoTrongNgayGio = coThamChieu ? Math.Round(laiLoTrongNgay, 0) : null;
        decimal? thayDoiTrongNgayGio = coThamChieu && giaTriThamChieu > 0m
            ? Math.Round(laiLoTrongNgay / giaTriThamChieu * 100m, 2)
            : null;

        var tyLe = giaTri / von;
        var muc = XacDinhMuc(tyLe, nguongCanhBao, nguongBan);

        // BUSINESS-RULE: thiếu giá thì chỉ cảnh báo sớm, không kết luận BÁN CẢ GIỎ trên dữ liệu khuyết (P4-19).
        if (muc == MucBao.Ban && thieuGia > 0)
            muc = MucBao.CanhBao;

        return new KetQuaTheoDoi(
            Math.Round(von, 0), Math.Round(giaTri, 0), Math.Round(tyLe, 4), muc, chiTiet, maLoNhat, thieuGia,
            Math.Round(giaTri - von, 0), laiLoTrongNgayGio, thayDoiTrongNgayGio);
    }

    private static MucBao XacDinhMuc(decimal tyLe, decimal nguongCanhBao, decimal nguongBan)
    {
        if (tyLe <= nguongBan)
            return MucBao.Ban;
        return tyLe <= nguongCanhBao ? MucBao.CanhBao : MucBao.BinhThuong;
    }
}
