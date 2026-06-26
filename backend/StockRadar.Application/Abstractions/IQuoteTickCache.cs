using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

public interface IQuoteTickCache
{
    IReadOnlyList<QuoteTickDto> GetQuotes();

    QuoteTickDto? GetQuote(string symbol);

    IndexTickDto? GetIndex();

    void SetQuotes(IReadOnlyList<QuoteTickDto> quotes);

    void SetIndex(IndexTickDto? index);
}
