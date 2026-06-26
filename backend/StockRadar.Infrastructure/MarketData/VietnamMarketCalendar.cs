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
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

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
}
