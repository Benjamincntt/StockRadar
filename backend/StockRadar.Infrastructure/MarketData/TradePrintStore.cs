using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;

namespace StockRadar.Infrastructure.MarketData;

internal sealed class TradePrintStore : ITradePrintStore
{
    private const int MaxStored = 500;
    private readonly object _lock = new();
    private readonly LinkedList<TradePrintDto> _prints = new();

    public void Add(TradePrintDto print)
    {
        lock (_lock)
        {
            _prints.AddFirst(print);
            while (_prints.Count > MaxStored)
                _prints.RemoveLast();
        }
    }

    public IReadOnlyList<TradePrintDto> GetRecent(int limit = 50)
    {
        var take = Math.Clamp(limit, 1, MaxStored);
        lock (_lock)
        {
            return _prints
                .OrderByDescending(p => p.Price * 1000m * p.Volume)
                .ThenByDescending(p => p.At)
                .Take(take)
                .ToList();
        }
    }
}
