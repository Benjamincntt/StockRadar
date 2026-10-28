using StockRadar.Domain.Enums;

namespace StockRadar.Domain.Services;

/// <summary>
/// Luật BÁN realtime V3 (Phụ lục A + FR-015..018). Đánh giá trên GIÁ KHỚP trong phiên,
/// KHÔNG chờ giá đóng cửa (V3-B1). Ngoại lệ: lệnh theo thời điểm (N3 ATO, hết hạn ATC).
/// "Chạm MAn" = giá khớp ≥ trung bình (n−1) giá đóng cửa trước (research 01 dòng 40).
/// </summary>
public static class V3LuatBanThucTe
{
    public enum LyDoThoat
    {
        /// <summary>Chưa thoát — giữ tiếp.</summary>
        Khong,
        /// <summary>Giá khớp chạm/vượt ngưỡng MA + phí.</summary>
        ChamMA,
        /// <summary>Lệnh hoảng loạn sâu: giá khớp ≥ giá vào × 1,15 (FR-015a) — tách khỏi "chạm MA".</summary>
        ChotLoiHoangLoan,
        /// <summary>Hết "giữ tối đa" → bán ATC phiên này.</summary>
        HetHanATC,
        /// <summary>N3: đúng lịch ATO phiên TDM=2 tháng sau.</summary>
        AtoTheoLich,
    }

    /// <summary>Quyết định thoát cho một vị thế ở một tick giá khớp.</summary>
    public sealed record KetQuaThoat(
        bool Thoat,
        LyDoThoat LyDo,
        /// <summary>Ngưỡng giá khớp tối thiểu phải đạt (0 nếu thoát theo thời điểm).</summary>
        decimal NguongGia);

    /// <summary>Số phiên MA cho từng kịch bản (KB4 dùng MA10 như KB1/KB2; N1 dùng MA20; N3 không dùng MA).</summary>
    public static int MaCuaKichBan(LoaiKichBanV3 loai) => loai switch
    {
        LoaiKichBanV3.KB1 => 10,
        LoaiKichBanV3.KB2 => 10,
        LoaiKichBanV3.KB4 => 10,
        LoaiKichBanV3.N1 => 20,
        _ => 0,
    };

    /// <summary>
    /// FR-015a: quyết định CỜ hoảng loạn sâu từ (loại kịch bản, BREADTH50 phiên tín hiệu, cấu hình).
    /// KB1/KB2/KB4/N1 có BREADTH50 NHỎ HẰNG ngưỡng (0,15); N3 không bao giờ. Cờ chỉ chốt lúc tạo vị thế.
    /// </summary>
    public static bool LaHoangLoanSau(LoaiKichBanV3 loai, decimal breadth50, V3CauHinh cfg) =>
        loai is LoaiKichBanV3.KB1 or LoaiKichBanV3.KB2 or LoaiKichBanV3.KB4 or LoaiKichBanV3.N1
        && breadth50 < cfg.HoangLoanSauBreadth50Max;

    /// <summary>FR-016a: giữ tối đa THEO VỊ THẾ — cờ hoảng loạn sâu bật → 40, không → theo kịch bản.</summary>
    public static int GiuToiDaTheoViThe(V3CauHinh cfg, LoaiKichBanV3 loai, bool laHoangLoanSau) =>
        laHoangLoanSau ? cfg.HoangLoanSauGiuToiDa : cfg.GiuToiDaCua(loai);

    /// <summary>Ngưỡng "chạm MAn" = trung bình (n−1) đóng cửa trước; thiếu dữ liệu → 0 (không chặn theo MA).</summary>
    public static decimal NguongChamMa(LoaiKichBanV3 loai, IReadOnlyList<decimal> closesTruocHienTai)
    {
        int n = MaCuaKichBan(loai);
        if (n <= 0)
            return 0m;
        int lay = n - 1;
        if (closesTruocHienTai.Count < lay)
            return 0m;
        decimal tong = 0m;
        for (var i = closesTruocHienTai.Count - lay; i < closesTruocHienTai.Count; i++)
            tong += closesTruocHienTai[i];
        return tong / lay;
    }

    /// <summary>
    /// Xét thoát một tick.
    /// </summary>
    /// <param name="denLichAtoN3">Hôm nay là phiên TDM=2 tháng sau cho vị thế N3 (chốt lịch, không cần giá).</param>
    /// <param name="daQuaTplus25">Đã tới buổi chiều phiên mua+2 trở đi (FR-017).</param>
    /// <param name="sanTrangHomNay">Phiên sàn trắng bên mua (H=L=giá sàn, đóng ở sàn) → KHÔNG được bán (FR-018).</param>
    /// <param name="soPhienDaGi">Số phiên đã giữ tính từ phiên tín hiệu (FR-016).</param>
    public static KetQuaThoat Xet(
        LoaiKichBanV3 loai,
        decimal giaVao,
        IReadOnlyList<decimal> closesTruocHienTai,
        decimal giaKhop,
        int soPhienDaGi,
        int giuToiDa,
        bool denLichAtoN3,
        bool daQuaTplus25,
        bool sanTrangHomNay,
        V3CauHinh cfg,
        bool laHoangLoanSau = false)
    {
        // N3: chỉ thoát theo lịch ATO (không có ngưỡng giá).
        if (loai == LoaiKichBanV3.N3)
        {
            if (denLichAtoN3)
                return new KetQuaThoat(true, LyDoThoat.AtoTheoLich, 0m);
            if (soPhienDaGi >= giuToiDa && !sanTrangHomNay && daQuaTplus25)
                return new KetQuaThoat(true, LyDoThoat.HetHanATC, 0m);
            return new KetQuaThoat(false, LyDoThoat.Khong, 0m);
        }

        // Sàn trắng bên mua: cấm bán phiên đó (kể cả hết hạn — phiên sau tính tiếp, đồng hồ giữ vẫn chạy).
        if (sanTrangHomNay)
            return new KetQuaThoat(false, LyDoThoat.Khong, 0m);

        // Hết "giữ tối đa": bán ATC phiên này (~14:25), cần qua T+2.5.
        if (soPhienDaGi >= giuToiDa)
            return daQuaTplus25
                ? new KetQuaThoat(true, LyDoThoat.HetHanATC, 0m)
                : new KetQuaThoat(false, LyDoThoat.Khong, 0m);

        // Thoát theo giá khớp: chưa qua T+2.5 thì chưa được bán.
        if (!daQuaTplus25)
            return new KetQuaThoat(false, LyDoThoat.Khong, 0m);

        // Lệnh hoảng loạn sâu: ngưỡng = giá vào × 1,15 (KHÔNG dùng MA). Lệnh thường: max(chạm MAn, giá vào + phí).
        decimal nguong = laHoangLoanSau
            ? giaVao * cfg.HoangLoanSauChotLoi
            : Math.Max(NguongChamMa(loai, closesTruocHienTai), giaVao * cfg.BanPhanTramGiaVao);
        return XetTheoNguongDaLuu(
            loai, nguong, giaKhop, soPhienDaGi, giuToiDa,
            denLichAtoN3, daQuaTplus25, sanTrangHomNay, laHoangLoanSau);
    }

    /// <summary>
    /// Cùng luật <see cref="Xet"/> nhưng nhận NGƯỠNG ĐÃ LƯU (nhịp mỗi phút không nạp lịch sử —
    /// ngưỡng "chạm MAn" do nhịp 16:00 tính sẵn và ghi vào vị thế).
    /// </summary>
    /// <param name="nguongDaLuu">Lệnh thường = max(TB n−1 đóng cửa trước, giá vào × (1+phí)); lệnh hoảng loạn sâu = giá vào × 1,15; 0 = chưa có ngưỡng.</param>
    /// <param name="laHoangLoanSau">Cờ vị thế (FR-015a) — bật thì lý do thoát khi chạm ngưỡng là ChotLoiHoangLoan, không phải ChamMA.</param>
    public static KetQuaThoat XetTheoNguongDaLuu(
        LoaiKichBanV3 loai,
        decimal nguongDaLuu,
        decimal giaKhop,
        int soPhienDaGi,
        int giuToiDa,
        bool denLichAtoN3,
        bool daQuaTplus25,
        bool sanTrangHomNay,
        bool laHoangLoanSau = false)
    {
        // N3: chỉ thoát theo lịch ATO (không có ngưỡng giá).
        if (loai == LoaiKichBanV3.N3)
        {
            if (denLichAtoN3)
                return new KetQuaThoat(true, LyDoThoat.AtoTheoLich, 0m);
            if (soPhienDaGi >= giuToiDa && !sanTrangHomNay && daQuaTplus25)
                return new KetQuaThoat(true, LyDoThoat.HetHanATC, 0m);
            return new KetQuaThoat(false, LyDoThoat.Khong, 0m);
        }

        // Sàn trắng bên mua: cấm bán phiên đó (kể cả hết hạn — phiên sau tính tiếp, đồng hồ giữ vẫn chạy).
        if (sanTrangHomNay)
            return new KetQuaThoat(false, LyDoThoat.Khong, 0m);

        // Hết "giữ tối đa": bán ATC phiên này (~14:25), cần qua T+2.5.
        if (soPhienDaGi >= giuToiDa)
            return daQuaTplus25
                ? new KetQuaThoat(true, LyDoThoat.HetHanATC, 0m)
                : new KetQuaThoat(false, LyDoThoat.Khong, 0m);

        // Thoát theo giá khớp: chưa qua T+2.5 thì chưa được bán.
        if (!daQuaTplus25)
            return new KetQuaThoat(false, LyDoThoat.Khong, nguongDaLuu);

        if (nguongDaLuu > 0m && giaKhop >= nguongDaLuu)
            return new KetQuaThoat(
                true, laHoangLoanSau ? LyDoThoat.ChotLoiHoangLoan : LyDoThoat.ChamMA, nguongDaLuu);
        return new KetQuaThoat(false, LyDoThoat.Khong, nguongDaLuu);
    }
}
