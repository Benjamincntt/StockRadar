namespace StockRadar.Application.Common;

/// <summary>Trạng thái phân tích Top cơ hội — phân biệt chưa chạy / 0 mã / có kết quả / list tham khảo.</summary>
public static class OpportunityAnalysisStatuses
{
    public const string NotRun = "not_run";
    public const string ZeroMatches = "zero_matches";
    public const string HasResults = "has_results";
    public const string ReferenceList = "reference_list";

    /// <summary>
    /// List ngày cũ chỉ khi phiên mục tiêu <em>chưa</em> phân tích (<see cref="NotRun"/>).
    /// Khi đã quét và <c>OpportunitiesSaved = 0</c> thì giữ rỗng → <see cref="ZeroMatches"/>
    /// (spec 004 — không gắn lại Top ngày trước).
    /// </summary>
    public static bool AllowPreviousDayList(int todayOpportunityCount, int? todayOpportunitiesSaved)
    {
        if (todayOpportunityCount > 0)
            return false;
        return todayOpportunitiesSaved != 0;
    }
}
