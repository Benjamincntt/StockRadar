using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Infrastructure.Notifications;

/// <summary>OHLCV prefetch cho vị thế VIP — mốc tham chiếu + nền trên.</summary>
internal sealed class VipPositionHistoryCache
{
    private readonly object _gate = new();
    private DateOnly _sessionDate;
    private readonly Dictionary<string, IReadOnlyList<OhlcvBar>> _bySymbol =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly DarvasBreakoutAnalyzer _darvas = new();

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
            var history = stock?.History as IReadOnlyList<OhlcvBar> ?? Array.Empty<OhlcvBar>();
            lock (_gate)
                _bySymbol[symbol] = history;
        }
    }

    public IReadOnlyList<OhlcvBar> GetHistory(string symbol)
    {
        lock (_gate)
        {
            return _bySymbol.TryGetValue(symbol, out var h) ? h : Array.Empty<OhlcvBar>();
        }
    }

    /// <summary>
    /// Mốc = max High trong cửa sổ lookback phiên gần nhất, không lùi xa hơn anchorWindowStart;
    /// gồm liveHigh phiên đang chạy.
    /// </summary>
    public static decimal ComputeAnchorPrice(
        IReadOnlyList<OhlcvBar> history,
        DateOnly anchorWindowStart,
        DateOnly sessionDate,
        int lookbackSessions,
        decimal liveHigh)
    {
        var lookback = Math.Max(1, lookbackSessions);
        var prior = history.Where(b => b.Date < sessionDate && b.Date >= anchorWindowStart).ToList();
        if (prior.Count > lookback)
            prior = prior.Skip(prior.Count - lookback).ToList();

        var anchor = liveHigh > 0 ? liveHigh : 0m;
        foreach (var bar in prior)
        {
            if (bar.High > anchor)
                anchor = bar.High;
        }

        return anchor;
    }

    public FlatBoxProfile? FindOverheadBox(
        string symbol,
        decimal entryPrice,
        DateOnly sessionDate,
        MasterAlertOptions cfg)
    {
        var history = GetHistory(symbol);
        if (history.Count == 0 || entryPrice <= 0)
            return null;

        var overheadCfg = DarvasBoxSettings.Default with
        {
            MaxBoxHeightPercent = cfg.OverheadBoxMaxHeightPercent,
            BreakoutMaxBoxHeightPercent = cfg.OverheadBoxMaxHeightPercent,
        };

        return _darvas.FindNearestOverheadBox(
            history,
            entryPrice,
            sessionDate,
            cfg.OverheadBoxMinSessions,
            Math.Max(cfg.OverheadBoxMinSessions, 45),
            cfg.OverheadBaseMaxAgeSessions,
            overheadCfg);
    }
}
