using System.Collections.Concurrent;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;

namespace StockRadar.Infrastructure.MarketData;

internal sealed class QuoteTickCache : IQuoteTickCache
{
    private readonly ConcurrentDictionary<string, QuoteTickDto> _quotes = new(StringComparer.OrdinalIgnoreCase);
    private IndexTickDto? _index;

    public IReadOnlyList<QuoteTickDto> GetQuotes() =>
        _quotes.Values.OrderBy(q => q.Symbol).ToList();

    public QuoteTickDto? GetQuote(string symbol) =>
        _quotes.TryGetValue(symbol.Trim().ToUpperInvariant(), out var quote) ? quote : null;

    public IndexTickDto? GetIndex() => _index;

    public void SetQuotes(IReadOnlyList<QuoteTickDto> quotes)
    {
        foreach (var quote in quotes)
            _quotes[quote.Symbol] = quote;
    }

    public void SetIndex(IndexTickDto? index) => _index = index;
}
