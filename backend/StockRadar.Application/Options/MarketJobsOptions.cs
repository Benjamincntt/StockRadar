namespace StockRadar.Application.Options;

/// <summary>
/// Pipeline thị trường (lên lịch bằng Quartz.NET):
/// Job 1 — backfill 2000-01-01 → T-1 (một lần)
/// Job 2 — append OHLCV phiên ngày T (sau đóng cửa)
/// Phân tích sau phiên — watchlist SmartMoney cho ngày mai (chạy sau Job 2)
/// Job 3 — monitor intraday 60s trong phiên ngày T+1
/// </summary>
public sealed class MarketJobsOptions
{
    public const string SectionName = "MarketJobs";

    public HistoryJobOptions History { get; set; } = new();
    public DailySessionJobOptions DailySession { get; set; } = new();
    public DailyAnalysisJobOptions DailyAnalysis { get; set; } = new();

    /// <summary>Job 2/3 đọc DB trực tiếp, không dùng memory cache API (stocks/index).</summary>
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

    /// <summary>TB khối lượng tối thiểu (N phiên gần nhất).</summary>
    public decimal MinAvgDailyVolume { get; set; } = 500_000m;

    public int VolumeLookbackSessions { get; set; } = 20;

    /// <summary>Giá đóng cửa tối thiểu (VND, ví dụ 8000).</summary>
    public decimal MinClosePriceVnd { get; set; } = 8_000m;

    /// <summary>Số phiên lấy mẫu trước khi lọc (IPO + volume).</summary>
    public int ScreeningLookbackDays { get; set; } = 450;

    /// <summary>Loại mã niêm yết/IPO trong N ngày gần nhất.</summary>
    public int ExcludeIpoWithinDays { get; set; } = 365;

    /// <summary>Job 1 không dùng memory cache KBS.</summary>
    public bool BypassCache { get; set; } = true;
}

/// <summary>Job 2: sau phiên 15h VN — append nến ngày T.</summary>
public sealed class DailySessionJobOptions
{
    public bool Enabled { get; set; } = true;

    public string Exchange { get; set; } = "HOSE";

    /// <summary>Giờ VN (sau phiên chiều ~14:45).</summary>
    public int Hour { get; set; } = 15;

    public int Minute { get; set; } = 0;

    /// <summary>Số mã mỗi lần gọi bảng giá KBS.</summary>
    public int BatchSize { get; set; } = 40;

    /// <summary>Chạy lặp mỗi N phút trong giờ giao dịch (0 = chỉ cron Hour:Minute).</summary>
    public int IntervalMinutes { get; set; }

    /// <summary>Cho phép chạy Job 2 ngoài giờ giao dịch khi dùng IntervalMinutes.</summary>
    public bool ForceRunOutsideHours { get; set; }
}

/// <summary>Phân tích sau Job 2 → DailyOpportunities cho phiên mai.</summary>
public sealed class DailyAnalysisJobOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Chạy ngay sau khi sync phiên xong (phút).</summary>
    public int DelayAfterSessionMinutes { get; set; } = 2;

    /// <summary>Chỉ lưu mã SmartMoney ≥ ngưỡng (Huy Hoàng checklist).</summary>
    public int MinScore { get; set; } = 60;

    /// <summary>Top N cơ hội cho ngày mai (0 = không giới hạn).</summary>
    public int MaxResults { get; set; } = 30;

    /// <summary>Khi strict SmartMoney = 0 mã, lưu top theo Buy Score (nới nhẹ).</summary>
    public bool RelaxedFallbackEnabled { get; set; } = true;

    /// <summary>Buy Score tối thiểu cho fallback (bỏ gate breakout/MA stack).</summary>
    public int FallbackMinScore { get; set; } = 45;

    /// <summary>Số mã tối đa khi dùng fallback.</summary>
    public int FallbackMaxResults { get; set; } = 15;

    /// <summary>Chờ tối thiểu giữa hai lần bấm phân tích thủ công (phút).</summary>
    public int ManualAnalysisCooldownMinutes { get; set; } = 15;

    /// <summary>Thêm một lần Job 2 + phân tích khi hết phiên sáng (nến nửa ngày).</summary>
    public bool MorningRunEnabled { get; set; }

    /// <summary>Giờ VN — mặc định 11:30 (sau ATC phiên sáng).</summary>
    public int MorningRunHour { get; set; } = 11;

    public int MorningRunMinute { get; set; } = 30;
}
