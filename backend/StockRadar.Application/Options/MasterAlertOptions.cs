namespace StockRadar.Application.Options;

public sealed class MasterAlertOptions
{
    public const string SectionName = "MasterAlerts";

    public bool Enabled { get; set; } = true;

    /// <summary>% tăng tối thiểu so đỉnh nền (entry.BaseHigh) cho Mua điểm 1.</summary>
    public decimal BuyPoint1MinChangePercent { get; set; } = 3m;

    /// <summary>% tăng tối đa so đỉnh nền cho Mua điểm 1 (chỉ bắn trong dải min–max).</summary>
    public decimal BuyPoint1MaxChangePercent { get; set; } = 4m;

    /// <summary>% tăng tối thiểu so đỉnh nền cho Mua điểm 2 (cùng phiên).</summary>
    public decimal BuyPoint2MinChangePercent { get; set; } = 6m;

    /// <summary>KL khớp tối thiểu (legacy — không dùng cho Master alerts paced volume).</summary>
    public long MinSessionVolume { get; set; } = 800_000;

    /// <summary>Projected volume ratio tối thiểu để xác nhận dòng tiền (so TB 20 phiên, điều chỉnh theo giờ).</summary>
    public decimal MinVolumeRatioPaced { get; set; } = 1.5m;

    /// <summary>KL tuyệt đối tối thiểu (floor bảo vệ mã siêu nhỏ). 0 = tắt.</summary>
    public long MinSessionVolumeFloor { get; set; } = 50_000;

    /// <summary>Lợi nhuận đỉnh từ giá mua điểm 1 để Cắt lỗ điểm 1.</summary>
    public decimal CutLoss1MinPeakGainPercent { get; set; } = 4m;

    /// <summary>Lợi nhuận đỉnh từ giá mua điểm 1 để Cắt hết.</summary>
    public decimal CutAllMinPeakGainPercent { get; set; } = 6.5m;

    public int CooldownMinutes { get; set; } = 15;
}

public sealed class OpportunityPerformanceOptions
{
    public const string SectionName = "OpportunityPerformance";

    public bool Enabled { get; set; } = true;

    /// <summary>Số phiên chờ trước khi đo T+2.5.</summary>
    public int ForwardSessions { get; set; } = 2;

    /// <summary>ForwardSessions + 0.5 (T+2.5 VN).</summary>
    public int MinSessionsBeforeMeasure { get; set; } = 3;

    public decimal SuccessThresholdPercent { get; set; } = 3m;

    public decimal FlatMinPercent { get; set; } = -1m;

    /// <summary>Tỷ lệ hỏng vượt ngưỡng → đề xuất xem lại bộ lọc.</summary>
    public decimal MaxFailedRatePercent { get; set; } = 45m;

    public int WeeklyReviewHour { get; set; } = 15;

    public int WeeklyReviewMinute { get; set; } = 30;

    /// <summary>Thứ 6 — review tuần.</summary>
    public DayOfWeek WeeklyReviewDay { get; set; } = DayOfWeek.Friday;
}
