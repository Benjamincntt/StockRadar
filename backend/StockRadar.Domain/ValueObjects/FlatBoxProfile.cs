namespace StockRadar.Domain.ValueObjects;

/// <summary>Hộp tích lũy phẳng (Darvas) — thay thế card nền giá cũ.</summary>
public sealed record FlatBoxProfile(
    bool HasValidBox,
    bool IsBreakoutConfirmed,
    decimal BoxLow,
    decimal BoxHigh,
    int SessionDays,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal GainFromBoxTopPercent,
    decimal SuggestedStopLoss,
    decimal? PriceGainPercent,
    decimal? VolumeMultiplier,
    string RefBoxPeriod)
{
    public static FlatBoxProfile None { get; } = new(
        false, false, 0, 0, 0, default, default, 0, 0, null, null, "");
}
