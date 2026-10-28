using StockRadar.Domain.Enums;

namespace StockRadar.Domain.Services;

/// <summary>
/// Cấu hình thuần cho bộ kịch bản V3 (application map từ V3Options sang đây, Domain không đọc appsettings).
/// Mọi ngưỡng đều ở đây — logic V3 không hardcode con số (FR-011).
/// </summary>
public sealed record V3CauHinh
{
    // Vũ trụ UNI (FR-001).
    /// <summary>Ngưỡng TV20 tối thiểu vào UNI (tỷ đồng).</summary>
    public decimal Tv20MinUni { get; init; } = 5m;
    /// <summary>Giá đóng cửa tối thiểu vào UNI (nghìn đồng).</summary>
    public decimal CloseMinUni { get; init; } = 5m;

    // Kịch bản.
    /// <summary>KB1: BREADTH50 dưới ngưỡng này thì thỏa điều kiện diện rộng.</summary>
    public decimal Kb1Breadth50Max { get; init; } = 0.20m;
    /// <summary>KB1: chỉ số dưới MA50 quá mức này (%) thì thỏa (vd 7 = dưới 7%).</summary>
    public decimal Kb1ChiSoDuoiMa50Min { get; init; } = 7m;
    /// <summary>KB1: TV20 tối thiểu (tỷ).</summary>
    public decimal Kb1Tv20Min { get; init; } = 30m;

    /// <summary>KB2: chỉ số dưới MA50 quá mức này (%).</summary>
    public decimal Kb2ChiSoDuoiMa50Min { get; init; } = 5m;
    /// <summary>KB2: TV20 tối thiểu (tỷ).</summary>
    public decimal Kb2Tv20Min { get; init; } = 100m;

    /// <summary>KB4: SECR20 dưới ngưỡng này (%).</summary>
    public decimal Kb4Secr20Max { get; init; } = -10m;
    /// <summary>KB4: TV20 tối thiểu (tỷ).</summary>
    public decimal Kb4Tv20Min { get; init; } = 20m;
    /// <summary>KB4: BREADTH50 dưới ngưỡng này.</summary>
    public decimal Kb4Breadth50Max { get; init; } = 0.35m;

    /// <summary>N1: SECR20 dưới ngưỡng này (%).</summary>
    public decimal N1Secr20Max { get; init; } = -12m;
    /// <summary>N1: TV20 tối thiểu (tỷ).</summary>
    public decimal N1Tv20Min { get; init; } = 10m;
    /// <summary>N1: LEFT tối đa (số phiên còn lại tới cuối tháng).</summary>
    public int N1LeftMax { get; init; } = 6;

    /// <summary>N3: LEFT đúng bằng giá trị này (5 phiên tới cuối tháng).</summary>
    public int N3Left { get; init; } = 5;
    /// <summary>N3: TV20 tối thiểu (tỷ).</summary>
    public decimal N3Tv20Min { get; init; } = 30m;
    /// <summary>N3: bỏ nếu chỉ số cách MA50 trong ±(giá trị này %).</summary>
    public decimal N3VungCamMa50 { get; init; } = 1.5m;

    // Bán realtime (FR-015) + phí.
    /// <summary>Điều kiện giá ≥ giá vào × (1 + phí tối thiểu này) — 0,004 = 0,4%.</summary>
    public decimal BanPhanTramGiaVao { get; init; } = 1.004m;

    // Ngoại lệ hoảng loạn sâu (FR-015a/FR-016a — research PH-21).
    /// <summary>BREADTH50 phiên tín hiệu NHỎ HẰNG ngưỡng này → lệnh hoảng loạn sâu (0,15; đúng 0,15 thì không).</summary>
    public decimal HoangLoanSauBreadth50Max { get; init; } = 0.15m;
    /// <summary>Lệnh hoảng loạn sâu: bán realtime khi giá khớp ≥ giá vào × hệ số này (1,15 = +15%).</summary>
    public decimal HoangLoanSauChotLoi { get; init; } = 1.15m;
    /// <summary>Lệnh hoảng loạn sâu: giữ tối đa số phiên này (40), hết hạn bán ATC.</summary>
    public int HoangLoanSauGiuToiDa { get; init; } = 40;

    // Giữ tối đa (phiên, tính từ phiên tín hiệu).
    public int GiuToiDaKb1 { get; init; } = 15;
    public int GiuToiDaKb2 { get; init; } = 15;
    public int GiuToiDaKb4 { get; init; } = 10;
    public int GiuToiDaN1 { get; init; } = 15;
    public int GiuToiDaN3 { get; init; } = 12;

    /// <summary>Số phiên T+2.5: sớm nhất bán buổi chiều phiên mua + giá trị này.</summary>
    public int Tplus25Sessions { get; init; } = 2;

    /// <summary>
    /// Mốc bắt đầu buổi chiều (FR-017): ĐÚNG phiên T+2 chỉ được bán từ giờ này trở đi,
    /// các phiên sau đó bán cả ngày.
    /// </summary>
    public TimeOnly MocPhieuChieuTplus2 { get; init; } = new(13, 0);

    // Danh mục (FR-019/FR-020).
    /// <summary>Tối đa số vị thế đồng thời.</summary>
    public int MaxViThe { get; init; } = 5;
    /// <summary>Top mỗi kịch bản mỗi ngày.</summary>
    public int TopMoiKichBan { get; init; } = 5;
    /// <summary>Tỉ trọng mỗi lệnh = giá trị danh mục / SoChiaViThe.</summary>
    public int SoChiaViThe { get; init; } = 5;
    /// <summary>Hệ số nhân cho KB1/KB2/N1.</summary>
    public decimal TrongSoNhan { get; init; } = 1.5m;

    /// <summary>Phí khứ hồi (0,004 = 0,4%), trừ một lần khi bán.</summary>
    public decimal PhiKhuriHoi { get; init; } = 0.004m;
    /// <summary>Thắng = lãi ròng > ngưỡng này (0,0005 = 0,05%).</summary>
    public decimal NguongThang { get; init; } = 0.0005m;

    /// <summary>Giữ tối đa theo kịch bản.</summary>
    public int GiuToiDaCua(LoaiKichBanV3 loai) => loai switch
    {
        LoaiKichBanV3.KB1 => GiuToiDaKb1,
        LoaiKichBanV3.KB2 => GiuToiDaKb2,
        LoaiKichBanV3.KB4 => GiuToiDaKb4,
        LoaiKichBanV3.N1 => GiuToiDaN1,
        LoaiKichBanV3.N3 => GiuToiDaN3,
        _ => GiuToiDaKb1
    };

    /// <summary>Hệ số tỉ trọng: KB1/KB2/N1 = TrongSoNhan, còn lại = 1.</summary>
    public decimal TrongSoCua(LoaiKichBanV3 loai) =>
        loai is LoaiKichBanV3.KB1 or LoaiKichBanV3.KB2 or LoaiKichBanV3.N1 ? TrongSoNhan : 1m;

    /// <summary>Nhịp mua: KB1/KB2 = mở cửa phiên sau, còn lại = ATC phiên tín hiệu.</summary>
    public bool MuaMoCua(LoaiKichBanV3 loai) =>
        loai is LoaiKichBanV3.KB1 or LoaiKichBanV3.KB2;

    /// <summary>Thứ tự ưu tiên danh mục: KB1(0) > KB2(1) > KB4(2) > N1(3) > N3(4).</summary>
    public int UuTienCua(LoaiKichBanV3 loai) => loai switch
    {
        LoaiKichBanV3.KB1 => 0,
        LoaiKichBanV3.KB2 => 1,
        LoaiKichBanV3.KB4 => 2,
        LoaiKichBanV3.N1 => 3,
        LoaiKichBanV3.N3 => 4,
        _ => 9
    };
}
