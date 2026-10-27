namespace StockRadar.Domain.Services;

/// <summary>
/// Lịch phiên theo tháng (research 01, dòng LEFT/TDM):
/// <c>LEFT</c> = số phiên còn lại tới cuối tháng (0 = phiên cuối);
/// <c>TDM</c> = thứ tự phiên trong tháng (0 = phiên đầu). Đếm theo lịch giao dịch THẬT
/// (<see cref="TradingSessionMath.IsTradingDay"/>), không tự đoán tháng.
/// </summary>
public static class V3LichPhien
{
    /// <summary>Danh sách ngày giao dịch trong tháng chứa <paramref name="nam"/>–<paramref name="thang"/>.</summary>
    public static List<DateOnly> PhienTrongThang(int nam, int thang)
    {
        var ds = new List<DateOnly>();
        var d = new DateOnly(nam, thang, 1);
        var hetThang = d.AddMonths(1);
        while (d < hetThang)
        {
            if (TradingSessionMath.IsTradingDay(d))
                ds.Add(d);
            d = d.AddDays(1);
        }
        return ds;
    }

    /// <summary>
    /// (LEFT, TDM) của một phiên. Nếu <paramref name="ngay"/> không phải ngày giao dịch
    /// hoặc không nằm trong tháng (dữ liệu lạ) → trả (0, 0).
    /// </summary>
    public static (int Left, int Tdm) LeftVaNgayTrongThang(DateOnly ngay)
    {
        var ds = PhienTrongThang(ngay.Year, ngay.Month);
        int idx = ds.IndexOf(ngay);
        if (idx < 0)
            return (0, 0);
        int tdm = idx;                    // 0 = phiên đầu tháng
        int left = ds.Count - 1 - idx;    // 0 = phiên cuối tháng
        return (left, tdm);
    }

    /// <summary>Phiên TDM = <paramref name="tdm"/> (0-based) của tháng; thiếu phiên → null.</summary>
    public static DateOnly? LayPhienTdm(int nam, int thang, int tdm)
    {
        var ds = PhienTrongThang(nam, thang);
        return tdm >= 0 && tdm < ds.Count ? ds[tdm] : null;
    }

    /// <summary>Phiên ATO tháng sau cho N3: phiên TDM=2 (0-based) của tháng liền sau tháng chứa phiên tín hiệu.</summary>
    public static DateOnly? NgayAtoThangSau(DateOnly ngayTinHieu)
    {
        var thangSau = new DateOnly(ngayTinHieu.Year, ngayTinHieu.Month, 1).AddMonths(1);
        return LayPhienTdm(thangSau.Year, thangSau.Month, 2);
    }

    /// <summary>Hôm nay có phải phiên TDM=2 của tháng không (đến nhịp bắn tin ATO N3 lúc 08:30)?</summary>
    public static bool LaPhienAtoN3(DateOnly ngay)
    {
        var (_, tdm) = LeftVaNgayTrongThang(ngay);
        return TradingSessionMath.IsTradingDay(ngay) && tdm == 2;
    }

    /// <summary>
    /// BUSINESS-RULE: cửa bán T+2.5 (FR-017). <paramref name="soPhienGiua"/> = số phiên giữa phiên khớp
    /// mua và hôm nay. Lớn hơn <paramref name="tplus25"/> (từ phiên T+3) → bán cả ngày. ĐÚNG bằng
    /// <paramref name="tplus25"/> = <b>đúng phiên T+2</b> → chỉ được bán từ đầu buổi chiều
    /// (<paramref name="mocPhieuChieu"/>, mặc định 13:00) vì buổi sáng hàng chưa về tài khoản.
    /// Nhỏ hơn <paramref name="tplus25"/> → chưa được bán.
    /// </summary>
    public static bool DaToiTplus25(
        int soPhienGiua,
        TimeOnly gioXet,
        int tplus25,
        TimeOnly mocPhieuChieu) =>
        soPhienGiua > tplus25 || (soPhienGiua == tplus25 && gioXet >= mocPhieuChieu);
}
