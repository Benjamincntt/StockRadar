using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Options;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Services.ReversalBounce;
using StockRadar.Infrastructure.Persistence;
using StockRadar.Infrastructure.Persistence.Mapping;

namespace StockRadar.Infrastructure.MarketData.Backtest;

/// <summary>
/// Replay chiến lược counter-trend "sóng hồi" trên OHLCV lịch sử. Tín hiệu chốt ở Close(T),
/// vào lệnh Open(T+1); bán chỉ từ T+3 (mặc định); mô phỏng chất sàn (defer/force), gap-cancel,
/// slippage &amp; phí. KHÔNG dùng High/Low phiên T+1 để xác nhận tín hiệu phiên T.
/// </summary>
internal sealed class ReversalBounceBacktestRunner(
    IJobStockRepository stocks,
    ApplicationDbContext db,
    IMarketBreadthAnalyzer breadthAnalyzer,
    IMarketRegimeClassifier regimeClassifier,
    IReversalBounceAnalyzer analyzer,
    ICounterTrendDecisionEngine decision,
    IOptions<ReversalBounceOptions> options,
    IOptions<ReversalBounceBacktestOptions> backtestOptions,
    ILogger<ReversalBounceBacktestRunner> logger) : IReversalBounceBacktestService
{
    public async Task<ReversalBounceBacktestReport> RunAsync(
        ReversalBounceBacktestRequest request,
        CancellationToken cancellationToken = default)
    {
        var opt = options.Value;
        var btOpt = backtestOptions.Value;
        var settings = opt.ToSettings();

        var from = request.From;
        var to = request.To;
        var minScore = request.MinScoreOverride;

        var universe = (await stocks.GetAllAsync(cancellationToken))
            .Where(s => s.IsActive && !s.TradingRestricted && s.History.Count >= opt.MinHistoryDays)
            .ToList();
        var indexHistory = await LoadIndexHistoryAsync(cancellationToken);

        var regimeByDate = BuildRegimeTimeline(universe, indexHistory, from, to, opt);

        // 1) Thu thập ứng viên Confirmed có trade plan trong [from, to].
        var candidates = new List<Candidate>();
        foreach (var stock in universe)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bars = stock.History.OrderBy(b => b.Date).ToList();
            for (var i = 0; i < bars.Count; i++)
            {
                var date = bars[i].Date;
                if (date < from || date > to)
                    continue;
                if (i + 1 >= bars.Count)
                    continue; // cần phiên T+1 để vào lệnh

                var regime = regimeByDate.GetValueOrDefault(date, MarketRegime.Normal);
                var analysis = analyzer.Analyze(stock, indexHistory, regime, 50m, date, settings);
                if (analysis.Setup.Stage != ReversalBounceStage.Confirmed)
                    continue;
                if (minScore is not null && analysis.Setup.TotalScore < minScore.Value)
                    continue;

                var signal = decision.Decide(analysis.Setup, analysis.Features, settings);
                if (signal.TradePlan is null)
                    continue;

                candidates.Add(new Candidate(
                    stock.Symbol, stock.Exchange, date, i, bars,
                    signal.TradePlan, analysis.Features.AtrPercent,
                    analysis.Setup.TotalScore, regime));
            }
        }

        // 2) Dedup theo setup + giới hạn tín hiệu/ngày (rank theo score).
        var selected = SelectSignals(candidates, btOpt);

        // 3) Mô phỏng fill/exit (dùng simulator thuần ở Domain).
        var trades = new List<ReversalBounceBacktestTradeRecord>();
        var floorDeferred = 0;
        var gapCancelled = 0;
        foreach (var c in selected)
        {
            var fill = ReversalBounceFillSimulator.Simulate(
                c.Bars, c.SignalIndex, c.Exchange, c.Plan, c.AtrPercent,
                settings.GapCancelAtrMultiple, settings.Trade, request.AllowDefensiveEarlyExit);

            floorDeferred += fill.FloorDeferrals;
            if (fill.ExitReason == ReversalBounceExitReasons.GapCancelled)
                gapCancelled++;

            trades.Add(new ReversalBounceBacktestTradeRecord(
                Symbol: c.Symbol,
                SignalDate: c.SignalDate,
                EntryDate: fill.EntryDate ?? c.SignalDate,
                EntryPrice: fill.EntryPrice,
                ExitPrice: fill.ExitPrice,
                ExitDate: fill.ExitDate,
                SessionsToExit: fill.SessionsToExit,
                ExitReason: fill.ExitReason,
                ReturnPercentGross: fill.ReturnPercentGross,
                ReturnPercentNet: fill.ReturnPercentNet,
                MaxFavorablePercent: fill.MaxFavorablePercent,
                MaxAdversePercent: fill.MaxAdversePercent,
                TotalScore: c.TotalScore,
                Regime: c.Regime.ToString()));
        }

        var entered = trades.Count(t => t.ExitReason != ReversalBounceExitReasons.GapCancelled);
        var exited = trades.Count(t => t.ExitPrice is not null);
        var closedNet = trades.Where(t => t.ExitPrice is not null).Select(t => t.ReturnPercentNet).ToList();
        var win = closedNet.Count(r => r >= 1m);
        var lose = closedNet.Count(r => r <= -0.5m);
        var flat = closedNet.Count - win - lose;

        var report = new ReversalBounceBacktestReport(
            From: from,
            To: to,
            TotalSetups: candidates.Count,
            EnteredTrades: entered,
            ExitedTrades: exited,
            FloorLockDeferredCount: floorDeferred,
            GapCancelledCount: gapCancelled,
            WinCount: win,
            FlatCount: flat,
            LoseCount: lose,
            WinRatePercent: closedNet.Count == 0 ? 0m : Math.Round(win / (decimal)closedNet.Count * 100m, 2),
            AvgReturnPercentGross: Avg(trades.Where(t => t.ExitPrice is not null).Select(t => t.ReturnPercentGross)),
            AvgReturnPercentNet: Avg(closedNet),
            AvgMfePercent: Avg(trades.Where(t => t.ExitPrice is not null).Select(t => t.MaxFavorablePercent)),
            AvgMaePercent: Avg(trades.Where(t => t.ExitPrice is not null).Select(t => t.MaxAdversePercent)),
            Trades: trades);

        logger.LogInformation(
            "ReversalBounce backtest {From}..{To}: setups={Setups}, entered={Entered}, win={Win}/{Closed} ({Rate}%), gapCancel={Gap}, floorDefer={Defer}.",
            from, to, candidates.Count, entered, win, closedNet.Count, report.WinRatePercent, gapCancelled, floorDeferred);

        return report;
    }

    private sealed record Candidate(
        string Symbol,
        string Exchange,
        DateOnly SignalDate,
        int SignalIndex,
        IReadOnlyList<OhlcvBar> Bars,
        ReversalBounceTradePlan Plan,
        decimal AtrPercent,
        decimal TotalScore,
        MarketRegime Regime);

    private static List<Candidate> SelectSignals(List<Candidate> candidates, ReversalBounceBacktestOptions btOpt)
    {
        // Dedup theo (Symbol, CapitulationLow-proxy) không có setupId ở đây → dedup theo (Symbol, InvalidationPrice)
        // (mỗi đợt đáy có invalidation gần như cố định). Ưu tiên phiên xác nhận sớm nhất.
        var enteredKeys = new HashSet<string>();
        var perDate = new Dictionary<DateOnly, int>();
        var selected = new List<Candidate>();

        foreach (var c in candidates.OrderBy(c => c.SignalDate).ThenByDescending(c => c.TotalScore))
        {
            var key = $"{c.Symbol}|{Math.Round(c.Plan.InvalidationPrice, 0)}";
            if (enteredKeys.Contains(key))
                continue;
            var count = perDate.GetValueOrDefault(c.SignalDate, 0);
            if (count >= btOpt.MaxSignalsPerDay)
                continue;

            enteredKeys.Add(key);
            perDate[c.SignalDate] = count + 1;
            selected.Add(c);
            if (selected.Count >= btOpt.MaxSetupsToSimulate)
                break;
        }

        return selected;
    }

    private Dictionary<DateOnly, MarketRegime> BuildRegimeTimeline(
        IReadOnlyList<Stock> universe,
        IReadOnlyList<OhlcvBar> indexHistory,
        DateOnly from,
        DateOnly to,
        ReversalBounceOptions opt)
    {
        var map = new Dictionary<DateOnly, MarketRegime>();
        if (indexHistory.Count == 0)
            return map;

        var warmupStart = from.AddDays(-40);
        var dates = indexHistory
            .Select(b => b.Date)
            .Where(d => d >= warmupStart && d <= to)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var thresholds = opt.ToRegimeThresholds();
        MarketBreadthSnapshot? prev = null;
        foreach (var date in dates)
        {
            var indexAsOf = indexHistory.Where(b => b.Date <= date).ToList();
            var universeAsOf = universe
                .Select(s => s with { History = s.History.Where(b => b.Date <= date).ToList() })
                .Where(s => s.History.Count > 0)
                .ToList();

            var metrics = breadthAnalyzer.Analyze(universeAsOf, indexAsOf, date);
            var snap = regimeClassifier.Classify(metrics, prev, thresholds);
            map[date] = snap.Regime;
            prev = snap;
        }

        return map;
    }

    private async Task<IReadOnlyList<OhlcvBar>> LoadIndexHistoryAsync(CancellationToken cancellationToken)
    {
        var entity = await db.MarketIndices.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Symbol == "VNINDEX", cancellationToken);
        if (entity is null || string.IsNullOrWhiteSpace(entity.HistoryJson))
            return [];
        return JsonSerializer.Deserialize<List<OhlcvBar>>(entity.HistoryJson, EntityMapper.JsonOptions) ?? [];
    }

    private static decimal Avg(IEnumerable<decimal> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? 0m : Math.Round(list.Average(), 2);
    }
}
