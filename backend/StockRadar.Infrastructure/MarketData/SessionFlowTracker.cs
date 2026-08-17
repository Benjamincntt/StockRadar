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

            // Snapshot foreign net lúc bước sang chiều (13:00) — MỘT lần/phiên/mã, trước khi cộng
            // delta của tick này. Dùng để đo "khối ngoại đã quay đầu bán từ đầu chiều" (slice 2),
            // khác SessionForeignNet là tổng lũy kế từ ATO.
            if (!state.AfternoonSnapshotTaken
                && VietnamMarketCalendar.NowVietnam().TimeOfDay >= new TimeSpan(13, 0, 0))
            {
                state.ForeignNetAtAfternoonStart = state.SessionForeignNet;
                state.AfternoonSnapshotTaken = true;
            }

            state.SessionForeignNet += foreignNetDelta;
            state.SessionPropNet += propDelta;
            state.LastBookImbalance = bookImbalance;
            state.SessionPressure = ComputePressure(
                state.SessionForeignNet,
                state.SessionPropNet,
                bookImbalance);
            state.UpdatedAt = DateTime.UtcNow;

            return ToSnapshot(sym, state);
        }
    }

    public SessionFlowSnapshot? Get(string symbol)
    {
        var sym = symbol.Trim().ToUpperInvariant();
        lock (_gate)
        {
            return _states.TryGetValue(sym, out var state) ? ToSnapshot(sym, state) : null;
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
                .Select(kv => ToSnapshot(kv.Key, kv.Value))
                .ToList();
        }
    }

    private static SessionFlowSnapshot ToSnapshot(string symbol, SymbolFlowState state) =>
        new(
            symbol,
            state.SessionForeignNet,
            state.SessionPropNet,
            state.LastBookImbalance,
            state.SessionPressure,
            state.UpdatedAt,
            state.AfternoonSnapshotTaken
                ? state.SessionForeignNet - state.ForeignNetAtAfternoonStart
                : null);

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
        public long ForeignNetAtAfternoonStart;
        public bool AfternoonSnapshotTaken;
    }
}

internal sealed record SessionFlowSnapshot(
    string Symbol,
    long SessionForeignNet,
    long SessionPropNet,
    long LastBookImbalance,
    decimal SessionPressure,
    DateTime UpdatedAt,
    /// <summary>Foreign net từ 13:00 tới giờ. Null = chưa qua 13:00 (chưa snapshot) — không phải "0".</summary>
    long? ForeignNetSinceAfternoon = null);
