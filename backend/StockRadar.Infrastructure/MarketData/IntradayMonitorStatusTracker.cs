namespace StockRadar.Infrastructure.MarketData;

internal sealed class IntradayMonitorStatusTracker
{
    private readonly object _gate = new();
    private DateTime? _lastScanAtUtc;
    private int _lastSymbolsScanned;
    private int _lastAlertsSent;
    private string? _lastSkipReason;

    public void RecordScan(DateTime utcNow, int symbolsScanned, int alertsSent)
    {
        lock (_gate)
        {
            _lastScanAtUtc = utcNow;
            _lastSymbolsScanned = symbolsScanned;
            _lastAlertsSent = alertsSent;
            _lastSkipReason = null;
        }
    }

    public void RecordSkipped(DateTime utcNow, string reason)
    {
        lock (_gate)
        {
            _lastSkipReason = reason;
        }
    }

    public (DateTime? LastScanAtUtc, int SymbolsScanned, int AlertsSent, string? SkipReason) Snapshot()
    {
        lock (_gate)
            return (_lastScanAtUtc, _lastSymbolsScanned, _lastAlertsSent, _lastSkipReason);
    }
}
