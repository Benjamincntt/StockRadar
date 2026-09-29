namespace StockRadar.Infrastructure.Scheduling;

internal static class QuartzJobIds
{
    public const string HistoryBackfill = "history-backfill";
    public const string DailySessionSync = "daily-session-sync";
    public const string DailyAnalysis = "daily-analysis";
    public const string KbsMarketSync = "kbs-market-sync";
    public const string IntradayScanner = "intraday-scanner";
    public const string OpportunityMonitor = "opportunity-monitor";
    public const string Pha1TruocPhien = "pha1-truoc-phien";
    public const string Pha2TrongPhien = "pha2-trong-phien";
    public const string Pha3DoLuong = "pha3-do-luong";
}
