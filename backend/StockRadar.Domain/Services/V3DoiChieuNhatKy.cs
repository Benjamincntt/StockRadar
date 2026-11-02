using StockRadar.Domain.Enums;

namespace StockRadar.Domain.Services;

/// <summary>
/// Đối chiếu vị thế V3 với lệnh thật trong Nhật ký giao dịch (spec 014, plan D3). Hàm THUẦN:
/// nhận số đang lưu của MỘT vị thế + toàn bộ lệnh nhật ký của tài khoản danh mục + cờ bỏ qua,
/// trả số cần ghi đè hoặc "không đổi". Không đọc DB, không ghi, không bắn tin.
/// Hạn nhập (C2) dựa trên nhịp chạy: mỗi chiều chỉ được xét một lần ở nhịp 16:00 của chính phiên
/// khớp, nên lệnh nhập/sửa/xoá sau hạn tự nhiên không còn nhịp nào đọc tới — không cần cột khoá.
/// </summary>
public static class V3DoiChieuNhatKy
{
    /// <summary>Giá trong nhật ký theo nghìn đ/cp (cùng đơn vị ViTheV3.GiaVao) → làm tròn 2 số.</summary>
    private const int SoThapPhanGia = 2;

    /// <summary>Nguồn tín hiệu V3 trong nhật ký (cột NguonTinHieu).</summary>
    public const string NguonV3 = "V3";

    /// <summary>Lý do thoát ghi lên vị thế khi owner đã bán thật trước tín hiệu.</summary>
    public const string LyDoBanTheoNhatKy = "BanTheoNhatKy";

    /// <summary>
    /// Một lệnh nhật ký đưa vào đối chiếu. NgayKhop là ngày khớp ĐÃ quy đổi sang giờ Việt Nam
    /// (phần quy đổi mốc UTC nằm ở tầng Infrastructure — Domain không biết múi giờ).
    /// </summary>
    public sealed record Lenh(
        string MaChungKhoan,
        /// <summary>Chiều lệnh: "Mua" / "Ban" (các chiều khác bỏ qua).</summary>
        string Chieu,
        DateOnly NgayKhop,
        /// <summary>Giá khớp (nghìn đ/cp).</summary>
        decimal Gia,
        decimal KhoiLuong,
        string? NguonTinHieu,
        /// <summary>Mã tín hiệu = TinHieuV3.Id dạng chuỗi.</summary>
        string? MaTinHieu);

    /// <summary>Số đang lưu của vị thế tại nhịp đối chiếu (chỉ phần luật cần).</summary>
    public sealed record ViThe(
        long TinHieuV3Id,
        string Symbol,
        TrangThaiViTheV3 TrangThai,
        DateOnly? NgayVao,
        decimal? GiaVao,
        decimal? KhoiLuong,
        DateOnly? NgayThoat,
        bool LaHoangLoanSau,
        /// <summary>Phần bán đã lấy theo nhật ký ở một nhịp trước — không áp lại (FR-010a).</summary>
        bool DoiChieuBan = false);

    /// <summary>Phần mua lấy theo nhật ký (FR-004).</summary>
    public sealed record KetQuaMua(
        decimal KhoiLuong,
        decimal GiaVao,
        decimal GiaTriLenh,
        /// <summary>Ngưỡng chốt lời cho lệnh hoảng loạn sâu = giá vào mới × hệ số; null = bước (2) của runner tính.</summary>
        decimal? NguongChamMaHoangLoan);

    /// <summary>Phần bán lấy theo nhật ký (FR-005), chỉ khi đã bán hết.</summary>
    public sealed record KetQuaBan(
        decimal GiaThoat,
        decimal PhanTramLoiNhuan,
        /// <summary>"Thang"/"Thua"/"HoaVon" — chấm lại bằng V3ChamKetQua.</summary>
        string XepLoai,
        DateOnly NgayThoat,
        /// <summary>true = vị thế còn DangGiu, runner phải đóng lệnh; false = đã DaThoat, chỉ ghi lại số.</summary>
        bool DongViThe);

    /// <summary>Kết quả đối chiếu một vị thế trong một nhịp.</summary>
    /// <param name="Mua">Phần mua ghi theo nhật ký (null = giữ số hệ thống).</param>
    /// <param name="Ban">Phần bán ghi theo nhật ký (null = giữ số hệ thống).</param>
    /// <param name="BiHuy">Chủ ý bỏ qua tín hiệu → huỷ cả vị thế lẫn tín hiệu (FR-008).</param>
    /// <param name="SoLenhLechMa">Lệnh gắn đúng tín hiệu nhưng sai mã CK — BỎ QUA, runner ghi log.</param>
    /// <param name="BanMotPhan">Có lệnh bán nhưng chưa bán hết → không áp phần bán (P2), runner ghi log.</param>
    public sealed record KetQua(
        KetQuaMua? Mua,
        KetQuaBan? Ban,
        bool BiHuy,
        int SoLenhLechMa,
        bool BanMotPhan)
    {
        /// <summary>Có thay đổi gì để ghi xuống vị thế không.</summary>
        public bool CoThayDoi => Mua is not null || Ban is not null || BiHuy;

        /// <summary>Không có gì để ghi (kể cả cảnh báo) — runner bỏ qua vị thế này.</summary>
        public static readonly KetQua KhongDoi = new(null, null, false, 0, false);
    }

    /// <summary>
    /// Xét một vị thế tại nhịp đối chiếu của ngày <paramref name="ngayDoiChieu"/>.
    /// </summary>
    /// <param name="vt">Số đang lưu của vị thế.</param>
    /// <param name="ngayDoiChieu">Phiên đang được đối chiếu (= hôm nay của nhịp 16:00, giờ VN).</param>
    /// <param name="cacLenh">Toàn bộ lệnh nhật ký của tài khoản danh mục (hàm tự lọc theo tín hiệu).</param>
    /// <param name="biBoQua">Chủ nhân đã bấm "bỏ qua" cho tín hiệu của vị thế này.</param>
    /// <param name="cfg">Cấu hình V3 (chỉ dùng hệ số chốt lời hoảng loạn sâu).</param>
    /// <returns>Số cần ghi đè, hoặc KetQua.KhongDoi khi không có lệnh hợp lệ.</returns>
    public static KetQua DoiChieu(
        ViThe vt,
        DateOnly ngayDoiChieu,
        IReadOnlyList<Lenh> cacLenh,
        bool biBoQua,
        V3CauHinh cfg)
    {
        var lenhMua = new List<Lenh>();
        var lenhBan = new List<Lenh>();
        int soLenhLechMa = 0;
        foreach (var l in cacLenh)
        {
            if (!string.Equals(l.NguonTinHieu, NguonV3, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(l.MaTinHieu, vt.TinHieuV3Id.ToString(), StringComparison.Ordinal))
                continue;
            // DO-NOT-CHANGE: chỉ áp lệnh khi mã CK trùng vị thế — bỏ điều kiện này thì nhập nhầm mã tín hiệu sẽ ghi đè nhầm vị thế khác.
            if (!string.Equals(l.MaChungKhoan, vt.Symbol, StringComparison.OrdinalIgnoreCase))
            {
                soLenhLechMa++;
                continue;
            }
            // Lệnh khớp ngày khác phiên đang đối chiếu → quá hạn, không tính (C2).
            if (l.NgayKhop != ngayDoiChieu)
                continue;
            if (l.Chieu == "Mua")
                lenhMua.Add(l);
            else if (l.Chieu == "Ban")
                lenhBan.Add(l);
        }

        // Bỏ qua (FR-008): huỷ vị thế CHƯA đóng mở hôm nay. Vị thế đã thoát mà huỷ sẽ mất tiền khỏi công thức danh mục.
        if (biBoQua && vt.TrangThai is TrangThaiViTheV3.ChoMua or TrangThaiViTheV3.DangGiu
                       && vt.NgayVao == ngayDoiChieu)
            return new KetQua(null, null, true, soLenhLechMa, false);

        KetQuaMua? mua = null;
        var giaVao = vt.GiaVao ?? 0m;
        var khoiLuong = vt.KhoiLuong ?? 0m;

        if (lenhMua.Count > 0
            && vt.TrangThai is TrangThaiViTheV3.DangGiu or TrangThaiViTheV3.DaThoat
            && vt.NgayVao == ngayDoiChieu)
        {
            var tongMua = lenhMua.Sum(l => l.KhoiLuong);
            if (tongMua > 0m)
            {
                // FR-004: khối lượng = Σ lệnh mua, giá vào = bình quân gia quyền theo khối lượng.
                giaVao = Math.Round(lenhMua.Sum(l => l.Gia * l.KhoiLuong) / tongMua, SoThapPhanGia);
                khoiLuong = tongMua;
                mua = new KetQuaMua(
                    tongMua,
                    giaVao,
                    Math.Round(tongMua * giaVao, SoThapPhanGia),
                    // FR-007: hoảng loạn sâu phải đặt ngưỡng theo giá vào NGAY TẠI bước đối chiếu —
                    // bước (2) của runner có DO-NOT-CHANGE nên không ghi đè lệnh này.
                    vt.LaHoangLoanSau ? giaVao * cfg.HoangLoanSauChotLoi : null);
            }
        }

        KetQuaBan? ban = null;
        var banMotPhan = false;
        // Phạm vi bán: vị thế còn DangGiu (owner bán trước tín hiệu), thoát đúng phiên này, hoặc hệ thống
        // đã đóng ở phiên trước mà phần bán chưa đối chiếu (owner bán thật muộn — FR-010a).
        var xetBan = vt.TrangThai == TrangThaiViTheV3.DangGiu
                     || (vt.TrangThai == TrangThaiViTheV3.DaThoat
                         && (vt.NgayThoat == ngayDoiChieu
                             || (vt.NgayThoat < ngayDoiChieu && !vt.DoiChieuBan)));
        if (lenhBan.Count > 0 && xetBan && khoiLuong > 0m)
        {
            var tongBan = lenhBan.Sum(l => l.KhoiLuong);
            if (tongBan >= khoiLuong)
            {
                var giaThoat = Math.Round(lenhBan.Sum(l => l.Gia * l.KhoiLuong) / tongBan, SoThapPhanGia);
                var (lai, xepLoai) = V3ChamKetQua.Cham(giaVao, giaThoat, cfg);
                ban = new KetQuaBan(
                    giaThoat,
                    Math.Round(lai * 100m, 4),
                    xepLoai.ToString(),
                    ngayDoiChieu,
                    vt.TrangThai == TrangThaiViTheV3.DangGiu);
            }
            else
            {
                // P2: bản đầu chỉ áp khi bán hết — bán một phần thì giữ số hệ thống, runner ghi cảnh báo.
                banMotPhan = true;
            }
        }

        if (mua is null && ban is null && soLenhLechMa == 0 && !banMotPhan)
            return KetQua.KhongDoi;
        return new KetQua(mua, ban, false, soLenhLechMa, banMotPhan);
    }
}
