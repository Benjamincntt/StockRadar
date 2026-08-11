namespace StockRadar.Domain.MasterAlerts;

/// <summary>
/// Tên bucket kết quả dùng cho code Realized P&amp;L mới.
/// Không refactor 2 bản <c>internal</c> đang lặp ở
/// <c>OpportunityPerformanceRunner.OutcomeBuckets</c> và <c>EfPerformanceRepositories.OutcomeBucketNames</c>
/// — giữ diff gọn, tránh đụng calibration/FP-mining/shadow.
/// </summary>
public static class OutcomeBucketNames
{
    public const string Good = "Good";
    public const string Flat = "Flat";
    public const string Failed = "Failed";
}
