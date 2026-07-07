using Microsoft.Extensions.Logging;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;

namespace StockRadar.Application.Services;

public sealed class SetupTrackBackfillService(
    IDailyOpportunityRepository opportunities,
    ISetupTrackRepository setupTracks,
    IOpportunityPerformanceService performance,
    ILogger<SetupTrackBackfillService> logger) : ISetupTrackBackfillService
{
    public async Task<SetupTrackBackfillResultDto> BackfillFromDailyOpportunitiesAsync(
        int days = 180,
        CancellationToken cancellationToken = default)
    {
        var lookback = Math.Clamp(days, 30, 365);
        var today = TradingCalendar.TodayVietnam();
        var fromDate = TradingSessionMath.SubtractTradingSessions(today, lookback);

        var rows = await opportunities.GetSinceAsync(fromDate, cancellationToken);
        if (rows.Count == 0)
        {
            return new SetupTrackBackfillResultDto(
                lookback,
                fromDate,
                0,
                0,
                0,
                0,
                "Không có DailyOpportunities trong khoảng thời gian yêu cầu.");
        }

        var dates = rows
            .Select(r => r.ForTradingDate)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var registered = 0;
        foreach (var date in dates)
        {
            var dayRows = rows.Where(r => r.ForTradingDate == date).ToList();
            var seeds = dayRows.Select(r => new OpportunityTrackSeed(
                r.Symbol,
                r.Rank,
                r.BuyScore ?? r.Score,
                r.Price,
                r.ChangePercent,
                r.PredictedHitPercent ?? 0m,
                r.SetupDna,
                ScoreBreakdownJson: null,
                r.TradeState,
                r.TradeStateReason)).ToList();

            await setupTracks.RegisterOpportunitiesAsync(date, seeds, cancellationToken);
            registered += seeds.Count;
        }

        var measured = await performance.MeasurePendingOutcomesAsync(CancellationToken.None);

        logger.LogInformation(
            "SetupTrack backfill: {Registered} seeds / {Dates} ngày, đo thêm {Measured} setup.",
            registered,
            dates.Count,
            measured);

        return new SetupTrackBackfillResultDto(
            lookback,
            fromDate,
            rows.Count,
            dates.Count,
            registered,
            measured,
            $"Đã backfill {registered} track từ {dates.Count} ngày; đo thêm {measured} setup T+2.5.");
    }
}
