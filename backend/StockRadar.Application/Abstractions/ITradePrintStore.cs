using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

public interface ITradePrintStore
{
    void Add(TradePrintDto print);

    IReadOnlyList<TradePrintDto> GetRecent(int limit = 50);
}
