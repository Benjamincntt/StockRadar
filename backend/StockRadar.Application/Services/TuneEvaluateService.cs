using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;

namespace StockRadar.Application.Services;

/// <summary>Headless replay — không ghi DB; dùng cho Optuna HPO.</summary>
public sealed class TuneEvaluateService(
    IBacktestService backtest,
    IOptions<TuneEvaluateOptions> options) : ITuneEvaluateService
{
    public async Task<TuneEvaluateResponse> EvaluateAsync(
        TuneEvaluateRequest request,
        CancellationToken cancellationToken = default)
    {
        var cfg = options.Value;
        var minPass = Math.Clamp(request.MinPassScore, 45, 90);
        var maxResults = Math.Clamp(request.MaxResults, 1, 30);
        var days = Math.Clamp(request.Days ?? cfg.DefaultDays, 10, 180);
        var hold = Math.Clamp(request.HoldSessions ?? cfg.HoldSessions, 2, 10);

        var result = await backtest.RunSmartMoneyAsync(
            new SmartMoneyBacktestRequestDto(
                Days: days,
                MaxPicksPerDay: maxResults,
                HoldSessions: hold,
                RelaxedFallback: false,
                MinScore: null,
                MinPassScore: minPass,
                Mode: SmartMoneyBacktestMode.Strict),
            cancellationToken);

        var summary = result.Summary;
        if (summary.TotalTrades == 0)
        {
            return new TuneEvaluateResponse(
                -1000m,
                0,
                0,
                0,
                0,
                summary.TradingDaysScanned,
                0,
                "Không có lệnh — tham số quá chặt hoặc thiếu dữ liệu.");
        }

        var hitRate = summary.WinRatePercent / 100m;
        var avgMfe = summary.AvgReturnPercent / 100m;
        var maxDd = summary.MaxDrawdownPercent / 100m;

        var fitness =
            cfg.HitRateWeight * hitRate
            + cfg.AvgMfeWeight * avgMfe
            - cfg.MaxDrawdownWeight * maxDd;

        if (summary.TotalTrades < cfg.MinTradesRequired)
        {
            fitness -= (cfg.MinTradesRequired - summary.TotalTrades) * cfg.LowTradePenaltyPerTrade;
        }

        return new TuneEvaluateResponse(
            Math.Round(fitness, 2),
            Math.Round(hitRate, 4),
            Math.Round(avgMfe, 4),
            Math.Round(maxDd, 4),
            summary.TotalTrades,
            summary.TradingDaysScanned,
            summary.DaysWithPicks,
            $"Backtest {summary.FromDate:yyyy-MM-dd}→{summary.ToDate:yyyy-MM-dd}, strict, hold {hold} phiên.");
    }
}
