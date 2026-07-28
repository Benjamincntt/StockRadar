namespace StockRadar.Application.Jobs;

/// <summary>Định nghĩa tĩnh một job pipeline (metadata cho màn hình Jobs).</summary>
/// <param name="JobId">Khớp <c>JobKey.Name</c> của Quartz để listener map đúng lần chạy theo lịch.</param>
/// <param name="FrequencyRank">Nhỏ hơn = chạy nhiều lần hơn → xếp lên trước.</param>
/// <param name="TriggerEndpoint">Path tương đối dưới <c>/market/jobs</c> để chạy thủ công.</param>
public sealed record JobDefinition(
    string JobId,
    string Name,
    string Description,
    string Schedule,
    int FrequencyRank,
    bool Triggerable,
    string TriggerEndpoint);

/// <summary>
/// Đăng ký 7 job pipeline theo lịch Quartz — nguồn cho <c>GET /market/jobs</c>
/// và thứ tự sắp xếp theo tần suất. JobId phải trùng <c>QuartzJobIds</c>.
/// </summary>
public static class JobCatalog
{
    public const string KbsMarketSync = "kbs-market-sync";
    public const string OpportunityMonitor = "opportunity-monitor";
    public const string IntradayScanner = "intraday-scanner";
    public const string DailySessionSync = "daily-session-sync";
    public const string DailyAnalysis = "daily-analysis";
    public const string WeeklyOpportunityReview = "weekly-opportunity-review";
    public const string HistoryBackfill = "history-backfill";

    public static readonly IReadOnlyList<JobDefinition> All = new[]
    {
        new JobDefinition(KbsMarketSync, "Đồng bộ giá KBS",
            "Cập nhật bảng giá realtime trong phiên.",
            "~60 giây (trong phiên)", 0, true, "kbs-sync"),
        new JobDefinition(OpportunityMonitor, "Monitor cơ hội (VIP)",
            "Theo dõi Top intraday, bắn cảnh báo VIP.",
            "~60 giây (trong phiên)", 1, true, "opportunity-monitor"),
        new JobDefinition(IntradayScanner, "Quét đột biến phiên",
            "Radar biến động giá / khối lượng bất thường.",
            "~2 phút (trong phiên)", 2, true, "intraday-scan"),
        new JobDefinition(DailySessionSync, "Đồng bộ phiên (Job 2)",
            "Append nến ngày T + cảnh báo phá hộp Darvas.",
            "~5 phút (trong phiên) + 15:00", 3, true, "session"),
        new JobDefinition(DailyAnalysis, "Phân tích ngày",
            "SmartMoney Top + tiêu chí + breadth + sóng hồi.",
            "11:30 & 15:05 (T2–T6)", 4, true, "analysis"),
        new JobDefinition(WeeklyOpportunityReview, "Review tuần",
            "Đo hiệu quả Top cơ hội cuối tuần.",
            "Thứ 6 15:30", 5, true, "weekly-review"),
        new JobDefinition(HistoryBackfill, "Universe & Backfill (Job 1)",
            "Lọc universe + tải lịch sử OHLCV.",
            "Thủ công", 6, true, "history"),
    };
}
