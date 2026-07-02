using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.MarketData;

internal sealed class IntradayMonitorStatusQueryService(
    IntradayMonitorStatusTracker tracker,
    IOptions<OpportunityMonitorOptions> options) : IIntradayMonitorStatusQuery
{
    public IntradayMonitorStatusDto GetStatus()
    {
        var cfg = options.Value;
        var interval = Math.Max(30, cfg.IntervalSeconds);
        var marketOpen = VietnamMarketCalendar.IsMarketOpen();
        var (lastScanUtc, symbols, alerts, skipReason) = tracker.Snapshot();

        var staleThreshold = TimeSpan.FromSeconds(interval * 2.5);
        var isStale = cfg.Enabled
            && marketOpen
            && lastScanUtc is not null
            && DateTime.UtcNow - lastScanUtc.Value > staleThreshold;

        var status = BuildStatus(cfg.Enabled, marketOpen, lastScanUtc, skipReason, isStale, interval);
        return new IntradayMonitorStatusDto(
            cfg.Enabled,
            marketOpen,
            interval,
            lastScanUtc,
            symbols,
            alerts,
            status,
            isStale);
    }

    private static string BuildStatus(
        bool enabled,
        bool marketOpen,
        DateTime? lastScanUtc,
        string? skipReason,
        bool isStale,
        int intervalSeconds)
    {
        if (!enabled)
            return "Quét khớp lệnh tắt trong cấu hình";

        if (!marketOpen)
        {
            if (lastScanUtc is null)
                return "Ngoài giờ giao dịch · chưa quét phiên này";

            return skipReason == "outside_hours"
                ? "Ngoài giờ giao dịch · chờ phiên tiếp theo"
                : "Ngoài giờ giao dịch";
        }

        if (lastScanUtc is null)
            return "Đang chờ lần quét đầu tiên trong phiên";

        if (isStale)
            return $"Chưa quét lại > {intervalSeconds * 2}s — kiểm tra API";

        return "Đang quét khớp lệnh trong phiên";
    }
}
