using StockRadar.Application.DTOs;

namespace StockRadar.Infrastructure.MarketData;

internal sealed class SessionFlowTracker
{
    private readonly object _gate = new();
    private DateOnly _sessionDate = VietnamMarketCalendar.TodayVietnam();
    private readonly Dictionary<string, SymbolFlowState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    public SessionFlowSnapshot Update(
        string symbol,
        long foreignNetDelta,
        long propDelta,
        long bookImbalance)
    {
        var sym = symbol.Trim().ToUpperInvariant();
        var today = VietnamMarketCalendar.TodayVietnam();

        lock (_gate)
        {
            if (today != _sessionDate)
            {
                _states.Clear();
                _sessionDate = today;
            }

            if (!_states.TryGetValue(sym, out var state))
            {
                state = new SymbolFlowState();
                _states[sym] = state;
            }

            state.SessionForeignNet += foreignNetDelta;
            state.SessionPropNet += propDelta;
            state.LastBookImbalance = bookImbalance;
            state.SessionPressure = ComputePressure(
                state.SessionForeignNet,
                state.SessionPropNet,
                bookImbalance);
            state.UpdatedAt = DateTime.UtcNow;

            return new SessionFlowSnapshot(
                sym,
                state.SessionForeignNet,
                state.SessionPropNet,
                state.LastBookImbalance,
                state.SessionPressure,
                state.UpdatedAt);
        }
    }

    public SessionFlowSnapshot? Get(string symbol)
    {
        var sym = symbol.Trim().ToUpperInvariant();
        lock (_gate)
        {
            if (!_states.TryGetValue(sym, out var state))
                return null;

            return new SessionFlowSnapshot(
                sym,
                state.SessionForeignNet,
                state.SessionPropNet,
                state.LastBookImbalance,
                state.SessionPressure,
                state.UpdatedAt);
        }
    }

    public IReadOnlyList<SessionFlowSnapshot> GetLeaders(int take, long minForeignNet = 0)
    {
        lock (_gate)
        {
            return _states
                .Where(kv => kv.Value.SessionForeignNet >= minForeignNet)
                .OrderByDescending(kv => kv.Value.SessionForeignNet)
                .Take(Math.Max(1, take))
                .Select(kv => new SessionFlowSnapshot(
                    kv.Key,
                    kv.Value.SessionForeignNet,
                    kv.Value.SessionPropNet,
                    kv.Value.LastBookImbalance,
                    kv.Value.SessionPressure,
                    kv.Value.UpdatedAt))
                .ToList();
        }
    }

    private static decimal ComputePressure(long foreignNet, long propNet, long bookImbalance)
    {
        var foreignPart = Math.Clamp(foreignNet / 50_000m, -40m, 40m);
        var propPart = Math.Clamp(propNet / 80_000m, -25m, 25m);
        var bookPart = Math.Clamp(bookImbalance / 100_000m, -35m, 35m);
        return Math.Round(foreignPart + propPart + bookPart, 1);
    }

    private sealed class SymbolFlowState
    {
        public long SessionForeignNet;
        public long SessionPropNet;
        public long LastBookImbalance;
        public decimal SessionPressure;
        public DateTime UpdatedAt;
    }
}

internal sealed record SessionFlowSnapshot(
    string Symbol,
    long SessionForeignNet,
    long SessionPropNet,
    long LastBookImbalance,
    decimal SessionPressure,
    DateTime UpdatedAt);
