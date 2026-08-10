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

/// <summary>Snapshot MA phiên trước + feature phụ trợ ML (fail-closed khi thiếu history).</summary>
internal sealed record VipPullbackMaContext(
    bool Available,
    decimal Ma10,
    decimal Ma20,
    decimal Ma50,
    bool UptrendLong,
    decimal? PriorClose5Ago = null,
    decimal AtrAbs = 0m,
    bool FeaturesComplete = false)
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

        decimal? close5Ago = prior.Count >= 6 ? prior[^6].Close : null;
        var atrAbs = ComputeAtrAbs(prior, 14);
        var featuresComplete = close5Ago is > 0 && atrAbs > 0;

        return new VipPullbackMaContext(
            true, ma10, ma20, ma50, uptrend, close5Ago, atrAbs, featuresComplete);
    }

    public decimal? LiveRs5dPercent(decimal liveClose)
    {
        if (PriorClose5Ago is not > 0 || liveClose <= 0)
            return null;
        return Math.Round((liveClose - PriorClose5Ago.Value) / PriorClose5Ago.Value * 100m, 2);
    }

    public decimal? LiveAtrPercent(decimal liveClose)
    {
        if (AtrAbs <= 0 || liveClose <= 0)
            return null;
        return Math.Round(AtrAbs / liveClose * 100m, 2);
    }

    public decimal? LiveDistMa20Percent(decimal liveClose)
    {
        if (!Available || Ma20 <= 0 || liveClose <= 0)
            return null;
        return Math.Round((liveClose - Ma20) / Ma20 * 100m, 2);
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

    private static decimal ComputeAtrAbs(IReadOnlyList<OhlcvBar> prior, int period)
    {
        if (prior.Count < 2)
            return 0m;
        var len = Math.Min(period, prior.Count - 1);
        var sum = 0m;
        for (var i = prior.Count - len; i < prior.Count; i++)
        {
            var prev = prior[i - 1].Close;
            var tr = Math.Max(prior[i].High - prior[i].Low,
                     Math.Max(Math.Abs(prior[i].High - prev), Math.Abs(prior[i].Low - prev)));
            sum += tr;
        }

        return sum / len;
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
