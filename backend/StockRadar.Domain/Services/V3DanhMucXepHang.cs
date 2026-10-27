using StockRadar.Domain.Enums;

namespace StockRadar.Domain.Services;

/// <summary>
/// Chọn lệnh theo danh mục V3 (FR-019/FR-020, Phụ lục C): tối đa 5 vị thế,
/// ưu tiên KB1 &gt; KB2 &gt; KB4 &gt; N1 &gt; N3, không trùng mã đang giữ, mỗi lệnh = V/5
/// (KB1/KB2/N1 ×1,5), không vượt tiền mặt. Một mã chỉ thuộc ĐÚNG MỘT kịch bản (V3-B4):
/// kịch bản có ưu tiên cao nhất thắng vòng loại.
/// </summary>
public static class V3DanhMucXepHang
{
    /// <summary>Trạng thái danh mục tại thời điểm chọn (nghìn đồng).</summary>
    public sealed record TrangThaiDanhMuc(
        /// <summary>Giá trị danh mục V = tiền mặt + Σ giá trị vị thế đang giữ.</summary>
        decimal GiaTriDanhMuc,
        /// <summary>Tiền mặt còn lại khả dụng.</summary>
        decimal TienMat,
        /// <summary>Số chỗ còn trống = MaxViThe − số vị thế đang giữ.</summary>
        int ChoTrong,
        /// <summary>Tập mã đang giữ (loại khỏi vòng chọn).</summary>
        IReadOnlySet<string> MaDangGiu);

    /// <summary>Một lệnh được chọn để mở.</summary>
    public sealed record LenhChon(
        string Symbol,
        LoaiKichBanV3 Loai,
        int HangTrongKichBan,
        /// <summary>Giá tham chiếu dùng tính quy mô (= giá tín hiệu).</summary>
        decimal GiaThamChieu,
        decimal HeSoTrongSo,
        /// <summary>Giá trị lệnh thực = min(V/5 × HệSố, tiền mặt còn).</summary>
        decimal GiaTriLenh);

    /// <summary>
    /// Chọn lệnh từ ứng viên đã chấm. Ứng viên là top-N mỗi kịch bản (đã xếp hạng nội bộ).
    /// Thuật toán: gom ứng viên, giữ kịch bản ưu tiên cao nhất cho mỗi mã, loại mã đang giữ,
    /// rồi tham lam theo (ưu tiên kịch bản, hạng) tới khi hết chỗ hoặc hết tiền mặt.
    /// </summary>
    public static IReadOnlyList<LenhChon> Chon(
        IReadOnlyList<V3BoKichBan.UngVien> ungVien,
        TrangThaiDanhMuc danhMuc,
        V3CauHinh cfg)
    {
        var ketQua = new List<LenhChon>();
        if (danhMuc.ChoTrong <= 0 || danhMuc.TienMat <= 0m || ungVien.Count == 0)
            return ketQua;

        // Mỗi mã chỉ lấy kịch bản ưu tiên cao nhất (UuTien nhỏ nhất), rồi hạng trong kịch bản đó.
        var maUuTien = new Dictionary<string, V3BoKichBan.UngVien>();
        foreach (var uv in ungVien)
        {
            if (danhMuc.MaDangGiu.Contains(uv.Symbol))
                continue;
            if (!maUuTien.TryGetValue(uv.Symbol, out var cu))
                maUuTien[uv.Symbol] = uv;
            else
            {
                int tu = cfg.UuTienCua(uv.Loai), tc = cfg.UuTienCua(cu.Loai);
                if (tu < tc || (tu == tc && uv.Hang < cu.Hang))
                    maUuTien[uv.Symbol] = uv;
            }
        }

        var ds = maUuTien.Values
            .OrderBy(u => cfg.UuTienCua(u.Loai))
            .ThenBy(u => u.Hang)
            .ToList();

        decimal tienMatCon = danhMuc.TienMat;
        int choCon = danhMuc.ChoTrong;

        foreach (var uv in ds)
        {
            if (choCon <= 0 || tienMatCon <= 0m)
                break;

            decimal heSo = cfg.TrongSoCua(uv.Loai);
            decimal mongMuon = danhMuc.GiaTriDanhMuc / cfg.SoChiaViThe * heSo;
            decimal giaTriLenh = Math.Min(mongMuon, tienMatCon);
            if (giaTriLenh <= 0m)
                continue;

            ketQua.Add(new LenhChon(
                uv.Symbol, uv.Loai, uv.Hang, uv.GiaTinHieu, heSo, giaTriLenh));
            tienMatCon -= giaTriLenh;
            choCon--;
        }

        return ketQua;
    }
}
