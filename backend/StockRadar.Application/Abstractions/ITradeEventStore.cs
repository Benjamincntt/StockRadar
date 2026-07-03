using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;

namespace StockRadar.Application.Abstractions;

public interface ITradeEventStore
{
    void Add(TradeEventDto tradeEvent);

    IReadOnlyList<TradeEventDto> GetRecent(int limit = 50, string? labelFilter = null);
}

public interface ISessionFlowQuery
{
    SessionFlowDto? GetSymbolFlow(string symbol);

    IReadOnlyList<FlowLeaderDto> GetLeaders(int take = 20);
}
