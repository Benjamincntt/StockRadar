namespace StockRadar.Infrastructure.Notifications;

internal sealed class IntradayAlertTracker
{
    private readonly object _gate = new();
    private readonly Dictionary<string, DateTime> _lastSent = new(StringComparer.OrdinalIgnoreCase);

    public bool ShouldSend(string symbol, string eventKey, TimeSpan cooldown)
    {
        var key = $"{symbol.Trim().ToUpperInvariant()}:{eventKey}";
        lock (_gate)
        {
            if (_lastSent.TryGetValue(key, out var last) && DateTime.UtcNow - last < cooldown)
                return false;
            _lastSent[key] = DateTime.UtcNow;
            return true;
        }
    }
}
