using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

/// <summary>Nhãn tiếng Việt cho nền giá (API flatBox).</summary>
public static class BasePriceLabels
{
    public const string Base = "Nền giá";
    public const string Breakout = "Phá vỡ nền giá";
    public const string BreakUp = "Nổ hướng lên";
    public const string BreakDown = "Gãy nền";

    public const decimal BreakUpMinPercent = 3m;

    public static string ResolveEventLabel(FlatBoxProfile box, decimal latestClose)
    {
        if (!box.HasValidBox)
            return Base;

        if (latestClose < box.BoxLow)
            return BreakDown;

        if (box.IsBreakoutConfirmed
            && box.PriceGainPercent is >= BreakUpMinPercent)
            return BreakUp;

        if (box.IsBreakoutConfirmed)
            return Breakout;

        return Base;
    }

    public static string FormatSignalTitle(string symbol, FlatBoxProfile box, decimal latestClose) =>
        $"📦 {symbol} — {ResolveEventLabel(box, latestClose)}";
}
