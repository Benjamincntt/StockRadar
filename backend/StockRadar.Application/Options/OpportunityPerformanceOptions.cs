namespace StockRadar.Application.Options;

/// <summary>Ngưỡng đo hiệu quả cơ hội T+2.5 + lịch review tuần, dùng cho đường Top/Shadow/backtest (GIỮ). Tách khỏi MasterAlertOptions.</summary>
public sealed class OpportunityPerformanceOptions
{
    public const string SectionName = "OpportunityPerformance";

    public bool Enabled { get; set; } = true;

    /// <summary>Số phiên chờ trước khi đo T+2.5.</summary>
    public int ForwardSessions { get; set; } = 2;

    /// <summary>ForwardSessions + 0.5 (T+2.5 VN).</summary>
    public int MinSessionsBeforeMeasure { get; set; } = 3;

    /// <summary>Win khi lãi T+2.5 ≥ ngưỡng (mặc định 1% — cover thuế/phí bán).</summary>
    public decimal SuccessThresholdPercent { get; set; } = 1m;

    /// <summary>Flat khi return ∈ [FlatMin, Success). Dưới FlatMin = Lose. Mặc định 0%.</summary>
    public decimal FlatMinPercent { get; set; } = 0m;

    /// <summary>Tỷ lệ hỏng vượt ngưỡng → đề xuất xem lại bộ lọc.</summary>
    public decimal MaxFailedRatePercent { get; set; } = 45m;

    public int WeeklyReviewHour { get; set; } = 15;

    public int WeeklyReviewMinute { get; set; } = 30;

    /// <summary>Thứ 6 — review tuần.</summary>
    public DayOfWeek WeeklyReviewDay { get; set; } = DayOfWeek.Friday;
}
