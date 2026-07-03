using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using Microsoft.Extensions.Options;
using StockRadar.Application.Options;

namespace StockRadar.Infrastructure.MarketData;

internal sealed class SessionFlowQueryService(
    SessionFlowTracker tracker,
    IOptions<OpportunityMonitorOptions> options) : ISessionFlowQuery
{
    public SessionFlowDto? GetSymbolFlow(string symbol)
    {
        var snap = tracker.Get(symbol);
        return snap is null ? null : ToDto(snap);
    }

    public IReadOnlyList<FlowLeaderDto> GetLeaders(int take = 20)
    {
        var min = options.Value.ForeignStrongSessionNet;
        return tracker.GetLeaders(take, minForeignNet: 0)
            .Select((s, i) => new FlowLeaderDto(
                s.Symbol,
                s.SessionForeignNet,
                s.SessionPropNet,
                s.SessionPressure,
                i + 1))
            .ToList();
    }

    private static SessionFlowDto ToDto(SessionFlowSnapshot snap) =>
        new(
            snap.Symbol,
            snap.SessionForeignNet,
            snap.SessionPropNet,
            snap.LastBookImbalance,
            snap.SessionPressure,
            snap.UpdatedAt);
}
