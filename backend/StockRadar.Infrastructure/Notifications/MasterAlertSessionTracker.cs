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
        /// <summary>Đã hydrate BuyPoint flags từ SQL trong phiên (tránh query mỗi tick).</summary>
        public bool SqlHydrated { get; set; }
        // TODO: fields bán dưới đây legacy — bán đã chuyển sang MasterAlertPositions (SQL)
        public bool CutLoss1Fired { get; set; }
        public bool CutAllFired { get; set; }

        private readonly Dictionary<string, int> _sellConfirmTicks = new(StringComparer.Ordinal);

        public int GetSellConfirm(string signal) =>
            _sellConfirmTicks.TryGetValue(signal, out var n) ? n : 0;

        public void BumpSellConfirm(string signal)
        {
            _sellConfirmTicks.TryGetValue(signal, out var n);
            _sellConfirmTicks[signal] = n + 1;
        }

        public void ResetSellConfirm(string signal) => _sellConfirmTicks.Remove(signal);

        public void ResetOtherSellConfirms(string keepSignal)
        {
            foreach (var key in _sellConfirmTicks.Keys.ToList())
            {
                if (!string.Equals(key, keepSignal, StringComparison.Ordinal))
                    _sellConfirmTicks.Remove(key);
            }
        }

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
