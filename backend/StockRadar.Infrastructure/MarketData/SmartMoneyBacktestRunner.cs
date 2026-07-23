using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Application.Services;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.Persistence;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>Replay SmartMoney trên OHLCV lịch sử — đo win rate / drawdown đa mã.</summary>
internal sealed class SmartMoneyBacktestRunner(
    IJobStockRepository stocks,
    ISmartMoneyOpportunitySelector smartMoney,
    IBuyDecisionEngine buyDecision,
    ApplicationDbContext db,
    AdaptiveScoringProfileFactory adaptiveProfileFactory,
    HitCalibrationProfileFactory hitCalibrationProfileFactory,
    IOptions<PriceRunupFilterOptions> runupFilter,
    IOptions<SmartMoneyOptions> smartMoneyOptions,
    IOptions<MarketJobsOptions> jobOptions,
    IOptions<OpportunityPerformanceOptions> performanceOptions,
    ILogger<SmartMoneyBacktestRunner> logger) : IBacktestService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<SmartMoneyBacktestResultDto> RunSmartMoneyAsync(
        SmartMoneyBacktestRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var days = Math.Clamp(request.Days, 5, 180);
        var maxPicks = Math.Clamp(request.MaxPicksPerDay, 1, 30);
        var holdSessions = Math.Clamp(request.HoldSessions, 2, 20);
        var cfg = jobOptions.Value.DailyAnalysis;
        var minScore = request.MinScore ?? cfg.MinScore;
        var relaxedMin = cfg.FallbackMinScore > 0 ? cfg.FallbackMinScore : 45;
        var relaxedMax = cfg.FallbackMaxResults > 0 ? cfg.FallbackMaxResults : maxPicks;
        var perf = performanceOptions.Value;

        var all = await stocks.GetAllAsync(cancellationToken);
        if (all.Count == 0)
            throw new Application.Common.AppException("DB trống", "Chạy Job 1/2 trước khi backtest.", 503);

        var indexHistory = await LoadIndexHistoryAsync(cancellationToken);
        var runup = runupFilter.Value.ToSettings();
        var sm = smartMoneyOptions.Value.ToSettings();
        if (request.MinPassScore is int passScore)
            sm = sm with { MinPassScore = passScore };
        var adaptive = await adaptiveProfileFactory.LoadAsync(cancellationToken);
        var calibration = await hitCalibrationProfileFactory.LoadAsync(cancellationToken);

        var latestSession = all
            .Where(s => s.History.Count > 0)
            .Select(s => s.History[^1].Date)
            .DefaultIfEmpty(DateOnly.FromDateTime(DateTime.UtcNow))
            .Max();

        if (indexHistory.Count > 0)
            latestSession = latestSession < indexHistory[^1].Date ? indexHistory[^1].Date : latestSession;

        var endDate = TradingSessionMath.SubtractTradingSessions(latestSession, holdSessions);
        var startDate = TradingSessionMath.SubtractTradingSessions(endDate, days - 1);

        var trades = new List<SmartMoneyBacktestTradeDto>();
        var daysWithPicks = 0;
        var scanned = 0;

        foreach (var asOfDate in TradingDaysBetween(startDate, endDate))
        {
            cancellationToken.ThrowIfCancellationRequested();
            scanned++;

            var universe = BuildUniverseAt(all, asOfDate);
            if (universe.Count < 10)
                continue;

            var indexAt = BuildIndexAt(indexHistory, asOfDate)
                ?? new MarketIndex("VNINDEX", 0, 0, 50, MarketTrend.Sideway, 0);

            var context = smartMoney.BuildContext(universe, indexAt, runup, sm, adaptive, calibration);
            var picks = SelectPicks(
                universe,
                context,
                sm,
                minScore,
                maxPicks,
                request.Mode,
                relaxedMin,
                relaxedMax);

            if (picks.Count == 0)
                continue;

            daysWithPicks++;
            foreach (var pick in picks)
            {
                var stock = universe.First(s => s.Symbol.Equals(pick.Symbol, StringComparison.OrdinalIgnoreCase));
                var entryPrice = stock.History[^1].Close;
                var exitPrice = TradingSessionMath.GetForwardPriceAtSessions(
                    FindFullHistory(all, pick.Symbol),
                    asOfDate,
                    holdSessions);

                if (exitPrice is null or <= 0)
                    continue;

                var ret = TradingSessionMath.GetForwardReturnPercent(entryPrice, exitPrice) ?? 0m;
                var outcome = ClassifyOutcome(ret, perf);
                trades.Add(new SmartMoneyBacktestTradeDto(
                    pick.Symbol,
                    asOfDate,
                    entryPrice,
                    exitPrice.Value,
                    ret,
                    pick.Score,
                    outcome,
                    pick.Relaxed));
            }
        }

        var summary = BuildSummary(
            startDate,
            endDate,
            scanned,
            daysWithPicks,
            trades,
            all.Count,
            request.Mode != SmartMoneyBacktestMode.Strict,
            perf.SuccessThresholdPercent);

        logger.LogInformation(
            "Backtest SmartMoney {From}→{To}: {Trades} lệnh, win {Win:0.#}%, avg {Avg:0.##}%.",
            startDate,
            endDate,
            summary.TotalTrades,
            summary.WinRatePercent,
            summary.AvgReturnPercent);

        return new SmartMoneyBacktestResultDto(summary, trades);
    }

    private List<PickCandidate> SelectPicks(
        IReadOnlyList<Stock> universe,
        SmartMoneyMarketContext context,
        SmartMoneySettings sm,
        int minScore,
        int maxPicks,
        SmartMoneyBacktestMode mode,
        int relaxedMin,
        int relaxedMax)
    {
        if (mode == SmartMoneyBacktestMode.Relaxed)
            return SelectRelaxedPicks(universe, context, maxPicks, relaxedMin, relaxedMax);

        var strict = SelectStrictPicks(universe, context, sm, minScore, maxPicks);
        if (strict.Count > 0 || mode == SmartMoneyBacktestMode.Strict)
            return strict;

        return SelectRelaxedPicks(universe, context, maxPicks, relaxedMin, relaxedMax);
    }

    private List<PickCandidate> SelectStrictPicks(
        IReadOnlyList<Stock> universe,
        SmartMoneyMarketContext context,
        SmartMoneySettings sm,
        int minScore,
        int maxPicks)
    {
        var strict = new List<PickCandidate>();
        foreach (var stock in universe)
        {
            var eval = smartMoney.Evaluate(stock, context);
            if (!smartMoney.PassesFilter(eval, sm))
                continue;
            if (minScore > 0 && eval.Score < minScore)
                continue;
            strict.Add(new PickCandidate(stock.Symbol, eval.Score, false));
        }

        return strict
            .OrderByDescending(p => p.Score)
            .ThenBy(p => p.Symbol, StringComparer.OrdinalIgnoreCase)
            .Take(maxPicks)
            .ToList();
    }

    private List<PickCandidate> SelectRelaxedPicks(
        IReadOnlyList<Stock> universe,
        SmartMoneyMarketContext context,
        int maxPicks,
        int relaxedMin,
        int relaxedMax)
    {
        var candidates = new List<PickCandidate>();
        foreach (var stock in universe)
        {
            var decision = buyDecision.Evaluate(stock, context);
            if (decision.GateFailure is not null
                && (decision.GateFailure.Contains("phân phối", StringComparison.OrdinalIgnoreCase)
                    || decision.GateFailure.Contains("FOMO", StringComparison.OrdinalIgnoreCase)))
                continue;
            if (decision.BuyScore <= 0)
                continue;
            candidates.Add(new PickCandidate(stock.Symbol, decision.BuyScore, true));
        }

        var take = Math.Min(maxPicks, relaxedMax);
        var aboveMin = candidates
            .Where(p => p.Score >= relaxedMin)
            .OrderByDescending(p => p.Score)
            .ThenBy(p => p.Symbol, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList();

        if (aboveMin.Count > 0)
            return aboveMin;

        return candidates
            .OrderByDescending(p => p.Score)
            .ThenBy(p => p.Symbol, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList();
    }

    private static SmartMoneyBacktestSummaryDto BuildSummary(
        DateOnly from,
        DateOnly to,
        int scanned,
        int daysWithPicks,
        IReadOnlyList<SmartMoneyBacktestTradeDto> trades,
        int universeSize,
        bool relaxedEnabled,
        decimal successThreshold)
    {
        if (trades.Count == 0)
        {
            return new SmartMoneyBacktestSummaryDto(
                from, to, scanned, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, universeSize, relaxedEnabled);
        }

        var returns = trades.Select(t => t.ReturnPercent).OrderBy(r => r).ToList();
        var wins = trades.Count(t => t.Outcome == "Good");
        var losses = trades.Count(t => t.Outcome == "Bad");
        var flats = trades.Count - wins - losses;
        var avg = Math.Round(returns.Average(), 2);
        var median = returns[returns.Count / 2];
        var maxDd = ComputeMaxDrawdown(trades);

        return new SmartMoneyBacktestSummaryDto(
            from,
            to,
            scanned,
            daysWithPicks,
            trades.Count,
            wins,
            losses,
            flats,
            Math.Round((decimal)wins / trades.Count * 100m, 1),
            avg,
            median,
            maxDd,
            successThreshold,
            universeSize,
            relaxedEnabled);
    }

    /// <summary>Drawdown trên chuỗi lợi nhuận ngày (bình quân các mã cùng ngày vào).</summary>
    private static decimal ComputeMaxDrawdown(IReadOnlyList<SmartMoneyBacktestTradeDto> trades)
    {
        var dailyReturns = trades
            .GroupBy(t => t.EntryDate)
            .OrderBy(g => g.Key)
            .Select(g => g.Average(t => t.ReturnPercent))
            .ToList();

        decimal equity = 100m;
        decimal peak = 100m;
        decimal maxDd = 0m;

        foreach (var dayRet in dailyReturns)
        {
            equity *= 1m + dayRet / 100m;
            if (equity > peak)
                peak = equity;
            var dd = peak > 0 ? (peak - equity) / peak * 100m : 0m;
            if (dd > maxDd)
                maxDd = dd;
        }

        return Math.Round(maxDd, 2);
    }

    private static string ClassifyOutcome(decimal returnPercent, OpportunityPerformanceOptions cfg)
    {
        if (returnPercent >= cfg.SuccessThresholdPercent)
            return "Good";
        if (returnPercent <= cfg.FlatMinPercent)
            return "Bad";
        return "Flat";
    }

    private static IReadOnlyList<Stock> BuildUniverseAt(IReadOnlyList<Stock> all, DateOnly asOfDate)
    {
        var list = new List<Stock>();
        foreach (var stock in all)
        {
            var idx = FindStockAsOfIndex(stock.History, asOfDate);
            if (idx < 0)
                continue;
            list.Add(stock with { History = stock.History.Take(idx + 1).ToList() });
        }

        return list;
    }

    private static IReadOnlyList<OhlcvBar> FindFullHistory(IReadOnlyList<Stock> all, string symbol) =>
        all.First(s => s.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)).History;

    private static MarketIndex? BuildIndexAt(IReadOnlyList<OhlcvBar> indexHistory, DateOnly asOfDate)
    {
        var idx = FindIndexAsOf(indexHistory, asOfDate);
        if (idx < 0)
            return null;

        var bar = indexHistory[idx];
        var prev = idx > 0 ? indexHistory[idx - 1] : bar;
        var change = prev.Close > 0
            ? Math.Round((bar.Close - prev.Close) / prev.Close * 100m, 2)
            : 0m;

        var idx5 = Math.Max(0, idx - 5);
        var bar5 = indexHistory[idx5];
        var change5 = bar5.Close > 0
            ? Math.Round((bar.Close - bar5.Close) / bar5.Close * 100m, 2)
            : change;

        var trend = change switch
        {
            > 0.5m => MarketTrend.Uptrend,
            < -0.5m => MarketTrend.Downtrend,
            _ => MarketTrend.Sideway,
        };

        var slice = indexHistory.Take(idx + 1).ToList();
        return new MarketIndex("VNINDEX", bar.Close, change, Score(change), trend, change5, slice);
    }

    private static int Score(decimal changePercent) =>
        Math.Clamp(50 + (int)(changePercent * 10), 0, 100);

    private async Task<IReadOnlyList<OhlcvBar>> LoadIndexHistoryAsync(CancellationToken cancellationToken)
    {
        var entity = await db.MarketIndices.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Symbol == "VNINDEX", cancellationToken);
        if (entity is null || string.IsNullOrWhiteSpace(entity.HistoryJson))
            return [];

        return JsonSerializer.Deserialize<List<OhlcvBar>>(entity.HistoryJson, JsonOptions) ?? [];
    }

    private static int FindIndexAsOf(IReadOnlyList<OhlcvBar> indexHistory, DateOnly asOfDate)
    {
        for (var i = indexHistory.Count - 1; i >= 0; i--)
        {
            if (indexHistory[i].Date <= asOfDate)
                return i;
        }

        return -1;
    }

    private static int FindStockAsOfIndex(IReadOnlyList<OhlcvBar> history, DateOnly asOfDate)
    {
        for (var i = history.Count - 1; i >= 0; i--)
        {
            if (history[i].Date == asOfDate)
                return i;
        }

        return -1;
    }

    private static IEnumerable<DateOnly> TradingDaysBetween(DateOnly from, DateOnly to)
    {
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (TradingSessionMath.IsTradingDay(d))
                yield return d;
        }
    }

    private sealed record PickCandidate(string Symbol, int Score, bool Relaxed);
}
