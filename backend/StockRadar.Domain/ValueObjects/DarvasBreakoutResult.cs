namespace StockRadar.Domain.ValueObjects;

/// <summary>Kết quả kiểm tra breakout từ hộp Darvas (phiên hiện tại vs nền kết thúc hôm qua).</summary>
public sealed record DarvasBreakoutResult(
    bool IsValidBreakout,
    decimal PriceGainPercent,
    decimal VolumeMultiplier,
    decimal ConfirmedBuyPrice,
    decimal SuggestedStopLoss,
    string RefBoxPeriod,
    decimal BoxMaxClose,
    decimal BoxMinClose,
    int BoxSessionCount)
{
    public static DarvasBreakoutResult Invalid { get; } = new(
        false, 0, 0, 0, 0, "", 0, 0, 0);
}
