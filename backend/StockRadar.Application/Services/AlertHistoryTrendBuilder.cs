using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

internal static class AlertHistoryTrendBuilder
{
    private const int MinDecidedForDelta = 3;

    public static AlertHistoryTrendsResponseDto Build(
        string period,
        IReadOnlyList<SetupTrackRecord> tracks,
        int limit,
        DateOnly? selectedPeriodStart)
    {
        period = NormalizePeriod(period);
        limit = Math.Clamp(limit, 4, 24);
        var today = TradingCalendar.TodayVietnam();
        var currentStart = GetPeriodStart(today, period);

        var grouped = tracks
            .GroupBy(t => GetPeriodStart(t.EntryDate, period))
            .ToDictionary(g => g.Key, g => g.ToList());

        var bucketStarts = grouped.Keys
            .OrderByDescending(x => x)
            .Take(limit)
            .OrderBy(x => x)
            .ToList();

        var buckets = new List<AlertHistoryTrendBucketDto>();
        decimal? prevWinRate = null;
        var prevDecidedEnough = false;

        foreach (var start in bucketStarts)
        {
            var rows = grouped[start];
            var end = GetPeriodEnd(start, period);
            var win = rows.Count(r => r.OutcomeBucket == "Good");
            var lose = rows.Count(r => r.OutcomeBucket == "Failed");
            var flat = rows.Count(r => r.OutcomeBucket == "Flat");
            var pending = rows.Count(r => !r.OutcomeMeasured);
            var decided = win + lose;
            var winRate = OpportunityPerformanceQueryService.ComputeOverallSuccessRatePercent(win, lose);
            var measuredReturns = rows
                .Where(r => r.OutcomeMeasured && r.ForwardReturnPercent is not null)
                .Select(r => r.ForwardReturnPercent!.Value)
                .ToList();
            decimal? avgReturn = measuredReturns.Count > 0
                ? Math.Round(measuredReturns.Average(), 2)
                : null;

            decimal? delta = null;
            if (prevWinRate is not null && prevDecidedEnough && decided > 0)
                delta = Math.Round(winRate - prevWinRate.Value, 1);

            var isSmall = decided < MinDecidedForDelta;
            buckets.Add(new AlertHistoryTrendBucketDto(
                start.ToString("yyyy-MM-dd"),
                FormatPeriodLabel(start, end, period),
                start,
                end,
                winRate,
                delta,
                win,
                lose,
                flat,
                pending,
                decided,
                isSmall,
                start == currentStart,
                avgReturn));

            prevWinRate = decided > 0 ? winRate : prevWinRate;
            prevDecidedEnough = decided >= MinDecidedForDelta;
        }

        var selected = selectedPeriodStart is not null
            ? buckets.LastOrDefault(b => b.PeriodStart == selectedPeriodStart.Value)
            : buckets.LastOrDefault(b => b.IsCurrentPeriod) ?? buckets.LastOrDefault();

        return new AlertHistoryTrendsResponseDto(period, selected, buckets);
    }

    internal static string NormalizePeriod(string period) =>
        period.Trim().ToLowerInvariant() switch
        {
            "month" or "thang" => "month",
            "quarter" or "quy" => "quarter",
            _ => "week",
        };

    internal static DateOnly GetPeriodStart(DateOnly date, string period) => period switch
    {
        "month" => new DateOnly(date.Year, date.Month, 1),
        "quarter" => new DateOnly(date.Year, ((date.Month - 1) / 3) * 3 + 1, 1),
        _ => CriterionReviewHelper.GetWeekStart(date),
    };

    internal static DateOnly GetPeriodEnd(DateOnly periodStart, string period) => period switch
    {
        "month" => periodStart.AddMonths(1).AddDays(-1),
        "quarter" => periodStart.AddMonths(3).AddDays(-1),
        _ => periodStart.AddDays(6),
    };

    private static string FormatPeriodLabel(DateOnly start, DateOnly end, string period) => period switch
    {
        "month" => $"{start.Month:00}/{start.Year}",
        "quarter" => $"Q{(start.Month - 1) / 3 + 1}/{start.Year}",
        _ => start.Month == end.Month
            ? $"Tuần {start:dd}–{end:dd}/{start:MM}/{start.Year}"
            : $"Tuần {start:dd/MM}–{end:dd/MM}",
    };
}
