namespace StockRadar.Application.Common;

public static class TradingCalendar
{
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    /// <summary>Offset cố định +07:00 cho ISO alert/session timestamps.</summary>
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static DateOnly TodayVietnam() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone));

    /// <summary>Hiển thị UTC trong giờ Việt Nam (dd/MM/yyyy HH:mm).</summary>
    public static string FormatVietnamDateTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(ToUtc(utc), VietnamTimeZone).ToString("dd/MM/yyyy HH:mm");

    /// <summary>Chỉ giờ:phút VN.</summary>
    public static string FormatVietnamTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(ToUtc(utc), VietnamTimeZone).ToString("HH:mm");

    public static DateTime ToUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    public static bool IsOnVietnamDate(DateTime utc, DateOnly date)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(ToUtc(utc), VietnamTimeZone);
        return DateOnly.FromDateTime(local) == date;
    }

    public static DateTime StartOfVietnamDayUtc(DateOnly date)
    {
        var localMidnight = date.ToDateTime(TimeOnly.MinValue);
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localMidnight, DateTimeKind.Unspecified),
            VietnamTimeZone);
    }

    public static bool IsTradingDay(DateOnly date) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    public static DateOnly NextTradingDay(DateOnly from)
    {
        var d = from.AddDays(1);
        while (!IsTradingDay(d))
            d = d.AddDays(1);
        return d;
    }

    /// <summary>Sau 15:10 VN hiển thị list ngày mai; trước đó list hôm nay.</summary>
    public static DateOnly GetActiveOpportunityDate() =>
        GetActiveOpportunityDate(new TimeSpan(15, 10, 0));

    /// <summary>Ngày phiên mà Job phân tích sau đóng cửa ghi vào DB (15:00 VN).</summary>
    public static DateOnly GetPostSessionAnalysisDate() =>
        GetActiveOpportunityDate(new TimeSpan(15, 0, 0));

    public static DateOnly GetActiveOpportunityDate(TimeSpan displayCutoff)
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
        var today = DateOnly.FromDateTime(now);

        if (!IsTradingDay(today))
            return NextTradingDay(today);

        return now.TimeOfDay >= displayCutoff
            ? NextTradingDay(today)
            : today;
    }

    /// <summary>Thời điểm tín hiệu kỹ thuật trên nến ngày (15:00 VN, hoặc UtcNow nếu là phiên hôm nay).</summary>
    public static DateTime GetSignalDetectedAt(DateOnly barDate)
    {
        var today = TodayVietnam();
        if (barDate == today)
            return DateTime.UtcNow;

        var localClose = barDate.ToDateTime(new TimeOnly(15, 0));
        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localClose, DateTimeKind.Unspecified),
            VietnamTimeZone);
    }
}
