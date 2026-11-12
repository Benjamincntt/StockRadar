namespace StockRadar.Application.Options;

/// <summary>
/// Pipeline thị trường (lên lịch bằng Quartz.NET):
/// Job 1 — backfill 2000-01-01 → T-1 (một lần)
/// Job 2 — append OHLCV phiên ngày T (sau đóng cửa)
/// </summary>
public sealed class MarketJobsOptions
{
    public const string SectionName = "MarketJobs";

    public HistoryJobOptions History { get; set; } = new();
    public DailySessionJobOptions DailySession { get; set; } = new();

    /// <summary>Job 2 đọc DB trực tiếp, không dùng memory cache API (stocks/index).</summary>
    public bool BypassApiCache { get; set; } = true;
}

public sealed class HistoryJobOptions
{
    /// <summary>Bật job backfill lịch sử (chạy thủ công hoặc RunOnStartup).</summary>
    public bool Enabled { get; set; }

    public bool RunOnStartup { get; set; }

    /// <summary>HOSE | Groups | AllListed</summary>
    public string Universe { get; set; } = "Groups";

    public string Exchange { get; set; } = "HOSE";

    public string[] Groups { get; set; } = ["HOSE", "HNX", "UPCOM"];

    public string StartDate { get; set; } = "2000-01-01";

    /// <summary>Chế độ chạy nhanh (nút thủ công ban ngày).</summary>
    public int DelayBetweenSymbolsMs { get; set; } = 300;

    /// <summary>Chế độ đêm — delay lớn hơn, giảm tải API.</summary>
    public int NightDelayBetweenSymbolsMs { get; set; } = 1500;

    public int VolumeLookbackSessions { get; set; } = 20;

    /// <summary>Giá đóng cửa tối thiểu (VND, ví dụ 8000).</summary>
    public decimal MinClosePriceVnd { get; set; } = 8_000m;

    /// <summary>Số phiên lấy mẫu trước khi lọc (IPO + volume).</summary>
    public int ScreeningLookbackDays { get; set; } = 450;

    /// <summary>Loại mã niêm yết/IPO trong N ngày gần nhất.</summary>
    public int ExcludeIpoWithinDays { get; set; } = 365;

    /// <summary>Job 1 không dùng memory cache KBS.</summary>
    public bool BypassCache { get; set; } = true;

    /// <summary>
    /// Chạy lại Job 1 định kỳ hàng tuần (chế độ đêm) — phá vòng chết mã bị loại nhầm/
    /// đủ điều kiện trở lại nhưng không có Job 2 để tự khôi phục (mã inactive không được
    /// append giá hàng ngày).
    /// </summary>
    public bool WeeklyRefreshEnabled { get; set; } = true;

    public DayOfWeek WeeklyRefreshDay { get; set; } = DayOfWeek.Sunday;

    public int WeeklyRefreshHour { get; set; } = 2;

    public int WeeklyRefreshMinute { get; set; }
}

/// <summary>Job 2: sau phiên 15h VN — append nến ngày T, kèm hai lần thử lại (spec 018 Q2/Q4).</summary>
public sealed class DailySessionJobOptions
{
    public bool Enabled { get; set; } = true;

    public string Exchange { get; set; } = "HOSE";

    /// <summary>Giờ VN (sau phiên chiều ~14:45).</summary>
    public int Hour { get; set; } = 15;

    public int Minute { get; set; } = 0;

    /// <summary>Số mã mỗi lần gọi bảng giá KBS.</summary>
    public int BatchSize { get; set; } = 40;

    /// <summary>
    /// Append nến phiên T cho cả mã đang inactive (không hạn chế GD) để giữ lịch sử "ấm".
    /// Phá vòng chết: mã bị loại khỏi universe sẽ không được Job 2 cập nhật giá → history đóng băng
    /// → rescreen chấm trên dữ liệu cũ nên không bao giờ tự khôi phục. Khi warm, rescreen có dữ liệu
    /// tươi để khôi phục mã đã đạt thanh khoản trở lại mà không cần chạy lại Job 1.
    /// </summary>
    public bool WarmInactiveUniverse { get; set; } = true;
}
