using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;

namespace StockRadar.Infrastructure.MarketData;

internal sealed class TradeEventStore(IOptions<OpportunityMonitorOptions> options) : ITradeEventStore
{
    private const int MaxStored = 500;
    private readonly object _lock = new();
    private readonly LinkedList<TradeEventDto> _events = new();

    public void Add(TradeEventDto tradeEvent)
    {
        lock (_lock)
        {
            _events.AddFirst(tradeEvent);
            while (_events.Count > MaxStored)
                _events.RemoveLast();
        }
    }

    public IReadOnlyList<TradeEventDto> GetRecent(int limit = 50, string? labelFilter = null)
    {
        var take = Math.Clamp(limit, 1, MaxStored);
        var foreignThreshold = options.Value.ForeignStrongSessionNet;

        lock (_lock)
        {
            IEnumerable<TradeEventDto> query = _events;

            if (!string.IsNullOrWhiteSpace(labelFilter)
                && !labelFilter.Equals(TradeEventLabels.FilterAll, StringComparison.OrdinalIgnoreCase))
            {
                if (labelFilter.Equals(TradeEventLabels.FilterForeignStrong, StringComparison.OrdinalIgnoreCase))
                    query = query.Where(e => e.SessionForeignNet >= foreignThreshold);
                else
                    query = query.Where(e =>
                        e.Label.Equals(labelFilter, StringComparison.OrdinalIgnoreCase));
            }

            return query
                .OrderByDescending(e => e.ValueVnd)
                .ThenByDescending(e => e.At)
                .Take(take)
                .ToList();
        }
    }
}
