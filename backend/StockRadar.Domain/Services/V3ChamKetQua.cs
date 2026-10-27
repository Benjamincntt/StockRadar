namespace StockRadar.Domain.Services;

/// <summary>
/// Chấm kết quả một lệnh V3 đã đóng (Phụ lục H + FR-024). Lãi ròng = (giá thoát/giá vào − 1) − phí
/// khứ hồi. Thắng = lãi ròng &gt; ngưỡng (0,05%); từ −ngưỡng đến +ngưỡng là HOÀ VỐN (không tính thắng)
/// — khớp research 01 dòng 56 &amp; lỗi "hoà vốn bị đếm thắng" (line 73).
/// </summary>
public static class V3ChamKetQua
{
    public enum KetQua { HoaVon, Thang, Thua }

    /// <summary>Lãi ròng (phần thập phân, đã trừ phí khứ hồi).</summary>
    public static decimal LaiRong(decimal giaVao, decimal giaThoat, V3CauHinh cfg)
    {
        if (giaVao <= 0m || giaThoat <= 0m)
            return 0m;
        return giaThoat / giaVao - 1m - cfg.PhiKhuriHoi;
    }

    public static KetQua XepLoai(decimal laiRong, V3CauHinh cfg)
    {
        if (laiRong > cfg.NguongThang)
            return KetQua.Thang;
        if (laiRong < -cfg.NguongThang)
            return KetQua.Thua;
        return KetQua.HoaVon;
    }

    public static (decimal LaiRong, KetQua XepLoai) Cham(decimal giaVao, decimal giaThoat, V3CauHinh cfg)
    {
        var lai = LaiRong(giaVao, giaThoat, cfg);
        return (lai, XepLoai(lai, cfg));
    }
}
