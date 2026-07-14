using StockRadar.Domain.Services;

namespace StockRadar.Infrastructure.MarketData;

internal static class VietnamMarketCalendar
{
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    public static DateTime NowVietnam() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);

    public static DateOnly TodayVietnam() =>
        DateOnly.FromDateTime(NowVietnam());

    public static bool IsTradingDay(DateOnly date) =>
        TradingSessionMath.IsTradingDay(date);

    public static DateOnly NextTradingDay(DateOnly from)
    {
        var d = from.AddDays(1);
        while (!IsTradingDay(d))
            d = d.AddDays(1);
        return d;
    }

    public static DateOnly PreviousTradingDay(DateOnly from)
    {
        var d = from.AddDays(-1);
        while (!IsTradingDay(d))
            d = d.AddDays(-1);
        return d;
    }

    /// <summary>Ngày giao dịch mà danh sách cơ hội đang hiển thị.</summary>
    public static DateOnly GetActiveOpportunityDate(TimeSpan analysisCutoff)
    {
        var now = NowVietnam();
        var today = DateOnly.FromDateTime(now);

        if (!IsTradingDay(today))
            return NextTradingDay(today);

        return now.TimeOfDay >= analysisCutoff
            ? NextTradingDay(today)
            : today;
    }

    public static DateTime GetNextRunUtc(int hour, int minute)
    {
        var now = NowVietnam();
        var runLocal = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
        if (now >= runLocal)
            runLocal = runLocal.AddDays(1);

        while (!IsTradingDay(DateOnly.FromDateTime(runLocal)))
            runLocal = runLocal.AddDays(1);

        return TimeZoneInfo.ConvertTimeToUtc(runLocal, VietnamTimeZone);
    }

    public static bool IsMarketOpen()
    {
        var now = NowVietnam();
        if (!IsTradingDay(DateOnly.FromDateTime(now)))
            return false;

        var t = now.TimeOfDay;
        var morning = t >= TimeSpan.FromHours(9) && t <= new TimeSpan(11, 30, 0);
        var afternoon = t >= TimeSpan.FromHours(13) && t <= new TimeSpan(14, 45, 0);
        return morning || afternoon;
    }

    /// <summary>
    /// % phiên giao dịch đã trôi qua (0.0–1.0).
    /// Sáng: 9:00–11:30 (150 phút), chiều: 13:00–14:45 (105 phút), tổng 255 phút.
    /// </summary>
    public static decimal SessionElapsedFraction()
    {
        var now = NowVietnam().TimeOfDay;

        var morningStart = new TimeSpan(9, 0, 0);
        var morningEnd = new TimeSpan(11, 30, 0);
        var afternoonStart = new TimeSpan(13, 0, 0);
        var afternoonEnd = new TimeSpan(14, 45, 0);
        const decimal totalMinutes = 255m;

        if (now < morningStart)
            return 0.01m;

        decimal elapsed;
        if (now <= morningEnd)
            elapsed = (decimal)(now - morningStart).TotalMinutes;
        else if (now < afternoonStart)
            elapsed = 150m;
        else if (now <= afternoonEnd)
            elapsed = 150m + (decimal)(now - afternoonStart).TotalMinutes;
        else
            elapsed = totalMinutes;

        elapsed = Math.Clamp(elapsed, 1m, totalMinutes);
        return Math.Round(elapsed / totalMinutes, 4);
    }
}
