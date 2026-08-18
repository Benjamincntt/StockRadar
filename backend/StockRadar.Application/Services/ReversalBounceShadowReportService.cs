using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Services;
using StockRadar.Domain.Services.ReversalBounce;

namespace StockRadar.Application.Services;

internal sealed class ReversalBounceShadowReportService(
    IReversalCandidateSnapshotRepository snapshots,
    IJobStockRepository stocks,
    IOptions<ReversalBounceOptions> options)
    : IReversalBounceShadowReportService
{
    public async Task<ReversalBounceShadowSummary> RunAsync(
        DateOnly from,
        DateOnly to,
        bool allowDefensiveEarlyExit = false,
        CancellationToken cancellationToken = default)
    {
        var opt = options.Value;
        var settings = opt.ToSettings();

        var actionable = await snapshots.GetActionableInRangeAsync(from, to, cancellationToken);
        var universe = (await stocks.GetAllAsync(cancellationToken))
            .GroupBy(s => s.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var inputs = new List<ReversalBounceShadowInput>(actionable.Count);
        foreach (var snap in actionable)
        {
            if (snap.TradePlan is null)
                continue;
            if (!universe.TryGetValue(snap.Symbol, out var stock))
                continue;

            var bars = stock.History.OrderBy(b => b.Date).ToList();
            var idx = bars.FindIndex(b => b.Date == snap.TradingDate);
            if (idx < 0)
                continue;

            inputs.Add(new ReversalBounceShadowInput(
                Symbol: snap.Symbol,
                SignalDate: snap.TradingDate,
                Regime: snap.MarketRegime,
                Exchange: stock.Exchange,
                Bars: bars,
                SignalIndex: idx,
                Plan: snap.TradePlan,
                AtrPercent: ComputeAtrPercent(bars, idx, opt.AtrWindow)));
        }

        return ReversalBounceShadowEvaluator.Evaluate(
            from, to, inputs, settings.GapCancelAtrMultiple, settings.Trade, allowDefensiveEarlyExit);
    }

    /// <summary>ATR% (phân số) tại phiên tín hiệu — dùng cho ngưỡng gap-cancel khi mô phỏng.</summary>
    private static decimal ComputeAtrPercent(IReadOnlyList<OhlcvBar> bars, int signalIndex, int window)
    {
        if (signalIndex < 1)
            return 0m;

        var atr = IndicatorMath.AtrAt(bars, signalIndex, window);
        var close = bars[signalIndex].Close;
        return close > 0m ? atr / close : 0m;
    }
}
