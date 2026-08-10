using StockRadar.Application.Abstractions;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>MA prefetch cho VIP pullback — fail-closed khi thiếu history.</summary>
internal sealed class VipPullbackMaCache
{
    private readonly object _gate = new();
    private DateOnly _sessionDate;
    private readonly Dictionary<string, VipPullbackMaContext> _bySymbol =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task PrefetchAsync(
        IEnumerable<string> symbols,
        DateOnly sessionDate,
        IJobStockRepository stocks,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_sessionDate != sessionDate)
            {
                _bySymbol.Clear();
                _sessionDate = sessionDate;
            }
        }

        foreach (var symbol in symbols.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_bySymbol.ContainsKey(symbol))
                    continue;
            }

            var stock = await stocks.GetBySymbolAsync(symbol, cancellationToken);
            var ctx = VipPullbackMaContext.FromHistory(stock?.History, sessionDate);
            lock (_gate)
                _bySymbol[symbol] = ctx;
        }
    }

    public VipPullbackMaContext Get(string symbol)
    {
        lock (_gate)
        {
            return _bySymbol.TryGetValue(symbol, out var ctx)
                ? ctx
                : VipPullbackMaContext.Unavailable;
        }
    }
}

/// <summary>Snapshot MA phiên trước — không bịa giá trị 0 để “near MA”.</summary>
internal sealed record VipPullbackMaContext(
    bool Available,
    decimal Ma10,
    decimal Ma20,
    decimal Ma50,
    bool UptrendLong)
{
    public static VipPullbackMaContext Unavailable { get; } = new(false, 0, 0, 0, false);

    public static VipPullbackMaContext FromHistory(
        IReadOnlyList<OhlcvBar>? history,
        DateOnly sessionDate)
    {
        if (history is null || history.Count == 0)
            return Unavailable;

        // Chỉ bar trước phiên hiện tại — tránh lẫn giá intraday.
        var prior = history.Where(b => b.Date < sessionDate).ToList();
        if (prior.Count < 50)
            return Unavailable;

        var ma10 = Sma(prior, 10);
        var ma20 = Sma(prior, 20);
        var ma50 = Sma(prior, 50);
        if (ma10 <= 0 || ma20 <= 0 || ma50 <= 0)
            return Unavailable;

        var lastClose = prior[^1].Close;
        // Fail-closed slope: thiếu data cho slope → không coi uptrend dài hạn.
        var slopeOk = prior.Count >= 20 + 3 && SignalAnalyzer.Ma20SlopeNonNegative(prior, 3);
        var uptrend = lastClose > ma50 && ma20 >= ma50 && slopeOk;

        return new VipPullbackMaContext(true, ma10, ma20, ma50, uptrend);
    }

    public bool IsNearMa(decimal liveClose, decimal nearPercent)
    {
        if (!Available || liveClose <= 0 || nearPercent <= 0)
            return false;

        var d10 = Math.Abs(liveClose - Ma10) / Ma10 * 100m;
        var d20 = Math.Abs(liveClose - Ma20) / Ma20 * 100m;
        return Math.Min(d10, d20) <= nearPercent;
    }

    public string NearMaLabel(decimal liveClose)
    {
        if (!Available || liveClose <= 0)
            return "MA";

        var d10 = Math.Abs(liveClose - Ma10) / Ma10 * 100m;
        var d20 = Math.Abs(liveClose - Ma20) / Ma20 * 100m;
        return d10 <= d20 ? "MA10" : "MA20";
    }

    private static decimal Sma(IReadOnlyList<OhlcvBar> bars, int period)
    {
        var count = Math.Min(period, bars.Count);
        var sum = 0m;
        for (var i = bars.Count - count; i < bars.Count; i++)
            sum += bars[i].Close;
        return sum / count;
    }
}
