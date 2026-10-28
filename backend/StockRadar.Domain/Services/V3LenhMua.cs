using StockRadar.Domain.Enums;

namespace StockRadar.Domain.Services;

/// <summary>
/// Luật thuần cho LỆNH MUA V3 (bổ sung v4 — research PH-22): quy mô theo lô (FR-020a)
/// và "phiên mua" của lệnh chờ mua (FR-014a). Không hardcode — lô lấy từ cấu hình (FR-011).
/// </summary>
public static class V3LenhMua
{
    /// <summary>
    /// FR-020a: khối lượng theo LôCoPhieu, LÀM TRÒN TỚI LÔ GẦN NHẤT (nửa lô lên), ÍT NHẤT 1 lô,
    /// và tổng tiền (khối lượng × giá) KHÔNG VƯỢT tiền mặt còn (vượt thì bớt từng lô).
    /// Bớt tới 0 lô (tiền mặt không đủ 1 lô) → trả 0 = bỏ mã. Đơn vị giá & tiền: nghìn đồng.
    /// </summary>
    public static decimal KhoiLuongTheoLo(decimal giaTriMongMuon, decimal gia, decimal tienMatCon, int lo)
    {
        if (gia <= 0m || lo <= 0 || tienMatCon < (decimal)lo * gia)
            return 0m; // giá sai hoặc không đủ 1 lô → bỏ mã

        var soLoMong = giaTriMongMuon <= 0m ? 0m : giaTriMongMuon / gia / lo;
        var soLo = Math.Round(soLoMong, MidpointRounding.AwayFromZero); // nửa lô làm tròn lên
        if (soLo < 1m)
            soLo = 1m; // ít nhất 1 lô

        // Bớt từng lô tới khi tổng tiền vừa tiền mặt còn.
        while (soLo > 0m && soLo * lo * gia > tienMatCon)
            soLo -= 1m;

        return soLo > 0m ? soLo * lo : 0m;
    }

    /// <summary>
    /// FR-014a: phiên mua của một lệnh chờ. KB1/KB2 (mở cửa phiên sau) = phiên giao dịch KẾ TIẾP
    /// sau phiên tín hiệu; KB4/N1/N3 (ATC phiên tín hiệu) = đúng phiên tín hiệu.
    /// </summary>
    public static DateOnly NgayPhienMua(LoaiKichBanV3 loai, V3CauHinh cfg, DateOnly ngayTinHieu) =>
        cfg.MuaMoCua(loai)
            ? TradingSessionMath.AddTradingSessions(ngayTinHieu, 1)
            : ngayTinHieu;

    /// <summary>Hôm nay ĐÚNG là phiên mua của lệnh chờ — chỉ khi đó lệnh chờ mới được khớp.</summary>
    public static bool LaDungPhienMua(LoaiKichBanV3 loai, V3CauHinh cfg, DateOnly ngayTinHieu, DateOnly homNay) =>
        homNay == NgayPhienMua(loai, cfg, ngayTinHieu);

    /// <summary>Đã tới/sau phiên mua (dùng lúc 16:00): phiên mua ≤ hôm nay → lệnh chờ hết hạn, huỷ.</summary>
    public static bool DaDenHoacQuaPhienMua(LoaiKichBanV3 loai, V3CauHinh cfg, DateOnly ngayTinHieu, DateOnly homNay) =>
        homNay >= NgayPhienMua(loai, cfg, ngayTinHieu);
}
