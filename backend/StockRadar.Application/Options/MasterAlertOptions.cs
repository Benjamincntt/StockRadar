namespace StockRadar.Application.Options;

public sealed class MasterAlertOptions
{
    public const string SectionName = "MasterAlerts";

    public bool Enabled { get; set; } = true;

    /// <summary>% tăng tối thiểu so đỉnh nền (entry.BaseHigh) cho Mua điểm 1.</summary>
    public decimal BuyPoint1MinChangePercent { get; set; } = 3m;

    /// <summary>% tăng tối thiểu so đỉnh nền cho Mua điểm 2 (cận trên BP1 = giá trị này).</summary>
    public decimal BuyPoint2MinChangePercent { get; set; } = 6m;

    /// <summary>KL khớp tối thiểu (legacy — không dùng cho Master alerts paced volume).</summary>
    public long MinSessionVolume { get; set; } = 800_000;

    /// <summary>Projected volume ratio tối thiểu cho Mua 1 nửa (so TB 20 phiên, điều chỉnh theo giờ).</summary>
    public decimal MinVolumeRatioPaced { get; set; } = 1.5m;

    /// <summary>Volume ratio tối thiểu riêng cho Mua hết (BuyPoint2).</summary>
    public decimal BuyPoint2MinVolumeRatio { get; set; } = 1.8m;

    /// <summary>Số chu kỳ quét liên tiếp giá giữ trên ngưỡng breakout trước khi bắn (~30–60s/chu kỳ).</summary>
    public int RequiredConfirmationTicks { get; set; } = 3;

    /// <summary>Sàn % phiên đã trôi khi tính paced volume (chống khuếch đại ATO đầu phiên). 0.2 ≈ 20% phiên.</summary>
    public decimal MinElapsedFractionForPacing { get; set; } = 0.2m;

    /// <summary>KL tuyệt đối tối thiểu (floor bảo vệ mã siêu nhỏ). 0 = tắt.</summary>
    public long MinSessionVolumeFloor { get; set; } = 50_000;

    /// <summary>Lợi nhuận đỉnh từ giá mua điểm 1 để Cắt lỗ điểm 1.</summary>
    public decimal CutLoss1MinPeakGainPercent { get; set; } = 4m;

    /// <summary>Lợi nhuận đỉnh từ giá mua điểm 1 để Cắt hết.</summary>
    public decimal CutAllMinPeakGainPercent { get; set; } = 6.5m;

    /// <summary>Lợi nhuận đỉnh tối thiểu (từ giá mua 1) để kích hoạt trailing stop động.</summary>
    public decimal TrailingStopMinPeak { get; set; } = 3m;

    /// <summary>% hồi từ đỉnh phiên để Cắt 1 nửa (nhân hệ số pha TT).</summary>
    public decimal BaseTrailingStopPercent1 { get; set; } = 2.5m;

    /// <summary>% hồi từ đỉnh phiên để Đóng vị thế (nhân hệ số pha TT).</summary>
    public decimal BaseTrailingStopPercent2 { get; set; } = 4.0m;

    public Dictionary<string, decimal> MarketPhaseMultipliers { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Favorable"] = 0.8m,
        ["Neutral"] = 1.0m,
        ["Unfavorable"] = 2.25m,
    };

    public int CooldownMinutes { get; set; } = 15;

    /// <summary>% trượt giá tối đa cho phép khi đặt lệnh đuổi — hiển thị trong Telegram buy alerts.</summary>
    public decimal SlippageBufferPercent { get; set; } = 1.5m;
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
