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
/// Đăng ký 4 job pipeline theo lịch Quartz — nguồn cho <c>GET /market/jobs</c>
/// và thứ tự sắp xếp theo tần suất. JobId phải trùng <c>QuartzJobIds</c>.
/// </summary>
public static class JobCatalog
{
    public const string KbsMarketSync = "kbs-market-sync";
    public const string DailySessionSync = "daily-session-sync";
    public const string HistoryBackfill = "history-backfill";

    public static readonly IReadOnlyList<JobDefinition> All = new[]
    {
        new JobDefinition(KbsMarketSync, "Đồng bộ giá KBS",
            "Cập nhật bảng giá realtime trong phiên.",
            "~60 giây (trong phiên)", 0, true, "kbs-sync"),
        new JobDefinition(DailySessionSync, "Đồng bộ phiên (Công việc 2)",
            "Append nến ngày T, gồm lần chạy 15:05 ghi nến đã có ATC.",
            "~5 phút (trong phiên) + 15:05", 1, true, "session"),
        new JobDefinition(HistoryBackfill, "Dữ liệu & Đồng bộ (Công việc 1)",
            "Lọc universe + tải lịch sử OHLCV.",
            "Thủ công", 2, true, "history"),
    };
}
