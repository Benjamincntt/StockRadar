namespace StockRadar.Infrastructure.Scheduling;

internal static class QuartzJobIds
{
    public const string HistoryBackfill = "history-backfill";
    public const string DailySessionSync = "daily-session-sync";
    public const string DailyAnalysis = "daily-analysis";
    public const string KbsMarketSync = "kbs-market-sync";
    public const string IntradayScanner = "intraday-scanner";
    public const string OpportunityMonitor = "opportunity-monitor";

    // Bộ kịch bản V3 (spec 012).
    public const string V3TruocPhien = "v3-truoc-phien";
    public const string V3TrongPhien = "v3-trong-phien";
    public const string V3Atc = "v3-atc";
    public const string V3SauPhien = "v3-sau-phien";
}
