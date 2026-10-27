using StockRadar.Domain.Enums;

namespace StockRadar.Domain.Services;

/// <summary>
/// Máy chấm 5 kịch bản V3 (Phụ lục A — chuẩn từ research 03). Mỗi kịch bản = điều kiện boolean
/// (cửa ngày toàn thị trường + cửa từng mã) + MỘT thước xếp hạng duy nhất → top 5/ngày (FR-010).
/// Không có điểm tổng hợp (V3-B2). Không hardcode ngưỡng — lấy từ <see cref="V3CauHinh"/> (FR-011).
/// </summary>
public static class V3BoKichBan
{
    /// <summary>Một ứng viên đạt điều kiện báo của một kịch bản, kèm hạng và snapshot chỉ báo (FR-024).</summary>
    public sealed record UngVien(
        string Symbol,
        LoaiKichBanV3 Loai,
        /// <summary>Hạng trong kịch bản, 1 = tốt nhất theo thước xếp hạng.</summary>
        int Hang,
        decimal GiaTinHieu,
        decimal GiaTriThuocXepHang,
        V3ChiSoToanThiTruong.ChiTieuMa ChiTieu,
        /// <summary>Nhịp mua: true = ATO/mở cửa phiên sau (KB1/KB2); false = ATC phiên tín hiệu (KB4/N1/N3).</summary>
        bool MuaMoCuaPhienSau);

    /// <summary>Trả danh sách ứng viên top-N của cả 5 kịch bản cho snapshot phiên tín hiệu.</summary>
    public static IReadOnlyList<UngVien> Cham(
        V3ChiSoToanThiTruong.KetQua thiTruong,
        V3CauHinh cfg)
    {
        var ketQua = new List<UngVien>();
        ketQua.AddRange(ChamKichBan(LoaiKichBanV3.KB1, thiTruong, cfg));
        ketQua.AddRange(ChamKichBan(LoaiKichBanV3.KB2, thiTruong, cfg));
        ketQua.AddRange(ChamKichBan(LoaiKichBanV3.KB4, thiTruong, cfg));
        ketQua.AddRange(ChamKichBan(LoaiKichBanV3.N1, thiTruong, cfg));
        ketQua.AddRange(ChamKichBan(LoaiKichBanV3.N3, thiTruong, cfg));
        return ketQua;
    }

    private static IEnumerable<UngVien> ChamKichBan(LoaiKichBanV3 loai, V3ChiSoToanThiTruong.KetQua kt, V3CauHinh cfg)
    {
        // Cửa toàn thị trường theo ngày (nếu kịch bản có).
        if (!CuaThiTruong(loai, kt, cfg))
            return [];

        var duDieu = kt.TheoMa.Values.Where(c => c.ThuocUni && CuaMa(loai, c, cfg));

        // Thước xếp hạng: (giá trị dùng để xếp, lấyTopCaoHayThap).
        return loai switch
        {
            LoaiKichBanV3.KB1 => XepHang(duDieu, c => c.Rs126, ascending: true, cfg.TopMoiKichBan, loai, true),
            LoaiKichBanV3.KB2 => XepHang(duDieu, c => c.Rsi14, ascending: true, cfg.TopMoiKichBan, loai, true),
            LoaiKichBanV3.KB4 => XepHang(duDieu, c => c.Ret20, ascending: true, cfg.TopMoiKichBan, loai, false),
            LoaiKichBanV3.N1 => XepHang(duDieu, c => c.Secr20 ?? decimal.MaxValue, ascending: true, cfg.TopMoiKichBan, loai, false),
            LoaiKichBanV3.N3 => XepHang(duDieu, c => c.Rs63, ascending: false, cfg.TopMoiKichBan, loai, false),
            _ => []
        };
    }

    private static bool CuaThiTruong(LoaiKichBanV3 loai, V3ChiSoToanThiTruong.KetQua kt, V3CauHinh cfg)
    {
        var duoiMa50 = kt.ChiSoDoLechMa50PhanTram; // âm = chỉ số dưới MA50
        switch (loai)
        {
            case LoaiKichBanV3.KB1:
                // BREADTH50 < ngưỡng HOẶC chỉ số dưới MA50 hơn ngưỡng.
                return kt.Breadth50 < cfg.Kb1Breadth50Max || duoiMa50 < -cfg.Kb1ChiSoDuoiMa50Min;
            case LoaiKichBanV3.KB2:
                return duoiMa50 < -cfg.Kb2ChiSoDuoiMa50Min;
            case LoaiKichBanV3.KB4:
                return kt.Breadth50 < cfg.Kb4Breadth50Max;
            case LoaiKichBanV3.N1:
                return kt.Left <= cfg.N1LeftMax;
            case LoaiKichBanV3.N3:
                // LEFT đúng bằng ngưỡng VÀ bỏ nếu chỉ số cách MA50 trong ±vùng.
                return kt.Left == cfg.N3Left && Math.Abs(duoiMa50) > cfg.N3VungCamMa50;
            default:
                return false;
        }
    }

    private static bool CuaMa(LoaiKichBanV3 loai, V3ChiSoToanThiTruong.ChiTieuMa c, V3CauHinh cfg)
    {
        switch (loai)
        {
            case LoaiKichBanV3.KB1:
                return c.Tv20Ty > cfg.Kb1Tv20Min;
            case LoaiKichBanV3.KB2:
                return c.Tv20Ty > cfg.Kb2Tv20Min;
            case LoaiKichBanV3.KB4:
                // Ngành bán quá mức + mã còn yếu hơn ngành + thanh khoản.
                // BUSINESS-RULE: SECR20 = null (mã chưa có ngành) → KHÔNG đủ điều kiện KB4 (FR-003).
                return c.Secr20 is decimal secrKb4 && secrKb4 <= cfg.Kb4Secr20Max && c.Ret20 < secrKb4 && c.Tv20Ty > cfg.Kb4Tv20Min;
            case LoaiKichBanV3.N1:
                // Đã có phiên sàn trong 20 nhưng hôm nay không sàn + ngành kiệt + thanh khoản.
                // BUSINESS-RULE: SECR20 = null (mã chưa có ngành) → KHÔNG đủ điều kiện N1 (FR-003),
                // không thay SECR20 bằng RET20 của chính mã.
                return c.Floor20 >= 1 && !c.LaPhienSan && c.Secr20 is decimal secrN1 && secrN1 <= cfg.N1Secr20Max && c.Tv20Ty > cfg.N1Tv20Min;
            case LoaiKichBanV3.N3:
                return c.Tv20Ty > cfg.N3Tv20Min;
            default:
                return false;
        }
    }

    private static IEnumerable<UngVien> XepHang(
        IEnumerable<V3ChiSoToanThiTruong.ChiTieuMa> duDieu,
        Func<V3ChiSoToanThiTruong.ChiTieuMa, decimal> thuoc,
        bool ascending,
        int top,
        LoaiKichBanV3 loai,
        bool muaMoCua)
    {
        var sapXep = ascending
            ? duDieu.OrderBy(thuoc)
            : duDieu.OrderByDescending(thuoc);

        var ds = sapXep.Take(top).ToList();
        var ketQua = new List<UngVien>(ds.Count);
        for (var i = 0; i < ds.Count; i++)
            ketQua.Add(new UngVien(
                ds[i].Symbol, loai, i + 1, ds[i].Close, thuoc(ds[i]), ds[i], muaMoCua));
        return ketQua;
    }
}
