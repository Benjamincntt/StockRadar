using StockRadar.Domain.Services;

namespace StockRadar.Application.Options;

/// <summary>
/// Cấu hình bộ kịch bản V3 (spec 012). Mọi ngưỡng đọc từ appsettings (FR-011);
/// <see cref="ToCauHinh"/> chuyển sang bản thuần Domain <see cref="V3CauHinh"/>.
/// </summary>
public class V3Options
{
    public const string SectionName = "V3";

    public bool Enabled { get; set; } = true;

    // Nhịp job.
    /// <summary>Giờ bắt đầu xét bán trong phiên (HH:mm).</summary>
    public string GioBatDau { get; set; } = "09:00";
    /// <summary>Giờ kết thúc xét bán trong phiên (HH:mm).</summary>
    public string GioKetThuc { get; set; } = "14:45";
    /// <summary>Chu kỳ chạy trong phiên (phút).</summary>
    public int IntervalPhut { get; set; } = 1;

    /// <summary>
    /// Ngày bắt đầu của lần nạp MỘT LẦN khi deploy (chuỗi chỉ số + UNI theo ngày vào ChiSoNgayV3).
    /// POST /api/v1/market/jobs/v3-nap-chi-so dùng giá trị này nếu không truyền ?tuNgay.
    /// </summary>
    public string TuNgayNapChiSo { get; set; } = "2023-01-01";

    // Vũ trụ UNI.
    public decimal Tv20MinUni { get; set; } = 5m;
    public decimal CloseMinUni { get; set; } = 5m;

    // KB1.
    public decimal Kb1Breadth50Max { get; set; } = 0.20m;
    public decimal Kb1ChiSoDuoiMa50Min { get; set; } = 7m;
    public decimal Kb1Tv20Min { get; set; } = 30m;

    // KB2.
    public decimal Kb2ChiSoDuoiMa50Min { get; set; } = 5m;
    public decimal Kb2Tv20Min { get; set; } = 100m;

    // KB4.
    public decimal Kb4Secr20Max { get; set; } = -10m;
    public decimal Kb4Tv20Min { get; set; } = 20m;
    public decimal Kb4Breadth50Max { get; set; } = 0.35m;

    // N1.
    public decimal N1Secr20Max { get; set; } = -12m;
    public decimal N1Tv20Min { get; set; } = 10m;
    public int N1LeftMax { get; set; } = 6;

    // N3.
    public int N3Left { get; set; } = 5;
    public decimal N3Tv20Min { get; set; } = 30m;
    public decimal N3VungCamMa50 { get; set; } = 1.5m;

    // Bán + phí.
    public decimal BanPhanTramGiaVao { get; set; } = 1.004m;

    // Ngoại lệ hoảng loạn sâu (FR-015a/FR-016a — research PH-21).
    public decimal HoangLoanSauBreadth50Max { get; set; } = 0.15m;
    public decimal HoangLoanSauChotLoi { get; set; } = 1.15m;
    public int HoangLoanSauGiuToiDa { get; set; } = 40;

    public int GiuToiDaKb1 { get; set; } = 15;
    public int GiuToiDaKb2 { get; set; } = 15;
    public int GiuToiDaKb4 { get; set; } = 10;
    public int GiuToiDaN1 { get; set; } = 15;
    public int GiuToiDaN3 { get; set; } = 12;
    public int Tplus25Sessions { get; set; } = 2;

    /// <summary>HH:mm — đúng phiên T+2 chỉ được bán từ mốc này (FR-017, T+2.5 = chiều phiên T+2).</summary>
    public string GioBanPhieuTplus2 { get; set; } = "13:00";

    // Danh mục.
    public int MaxViThe { get; set; } = 5;
    public int TopMoiKichBan { get; set; } = 5;
    public int SoChiaViThe { get; set; } = 5;
    public decimal TrongSoNhan { get; set; } = 1.5m;
    public decimal PhiKhuriHoi { get; set; } = 0.004m;
    public decimal NguongThang { get; set; } = 0.0005m;

    /// <summary>Vốn ban đầu chỉ để hiển thị % lãi (FR-020); không ảnh hưởng tín hiệu/tỉ trọng.</summary>
    public decimal VonBanDau { get; set; } = 1_000_000m;

    public V3CauHinh ToCauHinh() => new()
    {
        Tv20MinUni = Tv20MinUni,
        CloseMinUni = CloseMinUni,
        Kb1Breadth50Max = Kb1Breadth50Max,
        Kb1ChiSoDuoiMa50Min = Kb1ChiSoDuoiMa50Min,
        Kb1Tv20Min = Kb1Tv20Min,
        Kb2ChiSoDuoiMa50Min = Kb2ChiSoDuoiMa50Min,
        Kb2Tv20Min = Kb2Tv20Min,
        Kb4Secr20Max = Kb4Secr20Max,
        Kb4Tv20Min = Kb4Tv20Min,
        Kb4Breadth50Max = Kb4Breadth50Max,
        N1Secr20Max = N1Secr20Max,
        N1Tv20Min = N1Tv20Min,
        N1LeftMax = N1LeftMax,
        N3Left = N3Left,
        N3Tv20Min = N3Tv20Min,
        N3VungCamMa50 = N3VungCamMa50,
        BanPhanTramGiaVao = BanPhanTramGiaVao,
        HoangLoanSauBreadth50Max = HoangLoanSauBreadth50Max,
        HoangLoanSauChotLoi = HoangLoanSauChotLoi,
        HoangLoanSauGiuToiDa = HoangLoanSauGiuToiDa,
        GiuToiDaKb1 = GiuToiDaKb1,
        GiuToiDaKb2 = GiuToiDaKb2,
        GiuToiDaKb4 = GiuToiDaKb4,
        GiuToiDaN1 = GiuToiDaN1,
        GiuToiDaN3 = GiuToiDaN3,
        Tplus25Sessions = Tplus25Sessions,
        MocPhieuChieuTplus2 = TimeOnly.TryParse(GioBanPhieuTplus2, out var gioPhieu)
            ? gioPhieu
            : new TimeOnly(13, 0),
        MaxViThe = MaxViThe,
        TopMoiKichBan = TopMoiKichBan,
        SoChiaViThe = SoChiaViThe,
        TrongSoNhan = TrongSoNhan,
        PhiKhuriHoi = PhiKhuriHoi,
        NguongThang = NguongThang,
    };
}
