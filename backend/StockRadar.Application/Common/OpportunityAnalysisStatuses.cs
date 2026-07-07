namespace StockRadar.Application.Common;

/// <summary>Trạng thái phân tích Top cơ hội — phân biệt chưa chạy / 0 mã / có kết quả / list tham khảo.</summary>
public static class OpportunityAnalysisStatuses
{
    public const string NotRun = "not_run";
    public const string ZeroMatches = "zero_matches";
    public const string HasResults = "has_results";
    public const string ReferenceList = "reference_list";
}
