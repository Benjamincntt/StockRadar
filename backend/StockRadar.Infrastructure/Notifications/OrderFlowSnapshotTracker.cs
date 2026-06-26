using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.Notifications;

internal sealed class OrderFlowSnapshotTracker
{
    private readonly object _gate = new();
    private readonly Dictionary<string, KbsPriceBoardClient.KbsBoardRow> _last =
        new(StringComparer.OrdinalIgnoreCase);

    public KbsPriceBoardClient.KbsBoardRow? GetPrevious(string symbol)
    {
        var key = symbol.Trim().ToUpperInvariant();
        lock (_gate)
            return _last.TryGetValue(key, out var row) ? row : null;
    }

    public void Update(KbsPriceBoardClient.KbsBoardRow row)
    {
        lock (_gate)
            _last[row.Symbol] = row;
    }

    public void Reset()
    {
        lock (_gate)
            _last.Clear();
    }
}
