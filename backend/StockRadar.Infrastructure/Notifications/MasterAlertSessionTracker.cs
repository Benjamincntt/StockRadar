namespace StockRadar.Infrastructure.Notifications;

internal sealed class MasterAlertSessionTracker
{
    private readonly object _gate = new();
    private readonly Dictionary<string, SymbolMasterState> _states = new(StringComparer.OrdinalIgnoreCase);

    public SymbolMasterState GetOrReset(string symbol, DateOnly sessionDate)
    {
        var key = symbol.Trim().ToUpperInvariant();
        lock (_gate)
        {
            if (_states.TryGetValue(key, out var existing) && existing.SessionDate == sessionDate)
                return existing;

            var fresh = new SymbolMasterState(sessionDate);
            _states[key] = fresh;
            return fresh;
        }
    }

    internal sealed class SymbolMasterState(DateOnly sessionDate)
    {
        public DateOnly SessionDate { get; } = sessionDate;
        public bool EntryReadyFired { get; set; }
        public bool BuyPoint1Fired { get; set; }
        public decimal BuyPoint1Price { get; set; }
        public bool BuyPoint2Fired { get; set; }
        public int BuyPoint1ConfirmTicks { get; set; }
        public int BuyPoint2ConfirmTicks { get; set; }
        public decimal SessionHighSinceBuy1 { get; set; }
        public bool CutLoss1Fired { get; set; }
        public bool CutAllFired { get; set; }

        public void UpdateHigh(decimal high)
        {
            if (!BuyPoint1Fired)
                return;

            SessionHighSinceBuy1 = Math.Max(SessionHighSinceBuy1, high);
        }

        public decimal PeakGainPercent()
        {
            if (!BuyPoint1Fired || BuyPoint1Price <= 0)
                return 0;

            return Math.Round((SessionHighSinceBuy1 - BuyPoint1Price) / BuyPoint1Price * 100m, 2);
        }

        public decimal DrawdownFromPeak(decimal currentPrice)
        {
            if (!BuyPoint1Fired || BuyPoint1Price <= 0 || SessionHighSinceBuy1 <= 0)
                return 0m;

            var peak = PeakGainPercent();
            var currentGain = Math.Round(
                (currentPrice - BuyPoint1Price) / BuyPoint1Price * 100m, 2);

            return Math.Round(Math.Max(0m, peak - currentGain), 2);
        }
    }
}
