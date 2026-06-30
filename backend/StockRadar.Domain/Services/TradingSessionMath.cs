using StockRadar.Domain.Entities;

namespace StockRadar.Domain.Services;

public static class TradingSessionMath
{
    public static DateOnly AddTradingSessions(DateOnly from, int sessions)
    {
        var d = from;
        var remaining = sessions;
        while (remaining > 0)
        {
            d = d.AddDays(1);
            if (IsTradingDay(d))
                remaining--;
        }

        return d;
    }

    public static DateOnly SubtractTradingSessions(DateOnly from, int sessions)
    {
        var d = from;
        var remaining = sessions;
        while (remaining > 0)
        {
            d = d.AddDays(-1);
            if (IsTradingDay(d))
                remaining--;
        }

        return d;
    }

    public static bool IsTradingDay(DateOnly date) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    public static int TradingSessionsBetween(DateOnly from, DateOnly to)
    {
        if (to <= from)
            return 0;

        var count = 0;
        var d = from;
        while (d < to)
        {
            d = d.AddDays(1);
            if (IsTradingDay(d))
                count++;
        }

        return count;
    }

    public static decimal? GetCloseOnOrAfter(IReadOnlyList<OhlcvBar> history, DateOnly date)
    {
        foreach (var bar in history.OrderBy(b => b.Date))
        {
            if (bar.Date >= date)
                return bar.Close;
        }

        return null;
    }

    /// <summary>Giá T+2.5 = trung bình đóng cửa phiên T+2 và T+3.</summary>
    public static decimal? GetForwardPriceT25(IReadOnlyList<OhlcvBar> history, DateOnly entryDate)
    {
        var t2 = AddTradingSessions(entryDate, 2);
        var t3 = AddTradingSessions(entryDate, 3);
        var closeT2 = GetCloseOnOrAfter(history, t2);
        if (closeT2 is null)
            return null;

        var closeT3 = GetCloseOnOrAfter(history, t3);
        return closeT3 is null ? closeT2 : Math.Round((closeT2.Value + closeT3.Value) / 2m, 2);
    }

    public static decimal? GetForwardReturnPercent(
        decimal entryPrice,
        decimal? forwardPrice)
    {
        if (entryPrice <= 0 || forwardPrice is null or <= 0)
            return null;

        return Math.Round((forwardPrice.Value - entryPrice) / entryPrice * 100m, 2);
    }

    public static decimal? GetForwardPriceAtSessions(
        IReadOnlyList<OhlcvBar> history,
        DateOnly entryDate,
        int sessions) =>
        GetCloseOnOrAfter(history, AddTradingSessions(entryDate, sessions));

    public sealed record SwingPathMetrics(
        decimal? ReturnT5,
        decimal? ReturnT10,
        decimal MaxFavorableExcursionPercent,
        decimal MaxAdverseExcursionPercent);

    /// <summary>MFE/MAE và return T+5/T+10 trong window swing.</summary>
    public static SwingPathMetrics ComputeSwingPath(
        IReadOnlyList<OhlcvBar> history,
        DateOnly entryDate,
        decimal entryPrice,
        int shortSessions,
        int longSessions)
    {
        if (entryPrice <= 0)
            return new SwingPathMetrics(null, null, 0, 0);

        var ordered = history
            .Where(b => b.Date > entryDate)
            .OrderBy(b => b.Date)
            .Take(longSessions)
            .ToList();

        decimal mfe = 0;
        decimal mae = 0;
        foreach (var bar in ordered)
        {
            var highRet = (bar.High - entryPrice) / entryPrice * 100m;
            var lowRet = (bar.Low - entryPrice) / entryPrice * 100m;
            if (highRet > mfe)
                mfe = highRet;
            if (lowRet < mae)
                mae = lowRet;
        }

        decimal? ret5 = null;
        decimal? ret10 = null;
        if (ordered.Count >= shortSessions)
        {
            var close5 = ordered[shortSessions - 1].Close;
            ret5 = GetForwardReturnPercent(entryPrice, close5);
        }

        if (ordered.Count >= longSessions)
        {
            var close10 = ordered[longSessions - 1].Close;
            ret10 = GetForwardReturnPercent(entryPrice, close10);
        }

        return new SwingPathMetrics(
            ret5,
            ret10,
            Math.Round(mfe, 2),
            Math.Round(mae, 2));
    }
}
