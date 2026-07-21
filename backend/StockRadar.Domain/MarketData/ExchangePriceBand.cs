using StockRadar.Domain.Entities;

namespace StockRadar.Domain.MarketData;

/// <summary>
/// Tính ReferencePrice/Floor/Ceiling runtime từ OHLCV + Exchange (chưa lưu DB — spec §3.3).
/// Proxy floor-lock cho MVP; tinh chỉnh khi có data giá tham chiếu thật (0D).
/// </summary>
public static class ExchangePriceBand
{
    public const decimal Hose = 0.07m;
    public const decimal Hnx = 0.10m;
    public const decimal Upcom = 0.15m;

    /// <summary>Giá tham chiếu = Close phiên liền trước <paramref name="asOfDate"/>.</summary>
    public static decimal GetReferencePrice(IReadOnlyList<OhlcvBar> history, DateOnly asOfDate)
    {
        decimal prevClose = 0m;
        for (var i = 0; i < history.Count; i++)
        {
            if (history[i].Date < asOfDate)
                prevClose = history[i].Close;
            else
                break;
        }
        return prevClose;
    }

    public static decimal BandFor(string? exchange)
    {
        var ex = exchange?.Trim().ToUpperInvariant() ?? "";
        if (ex.Contains("HNX")) return Hnx;
        if (ex.Contains("UPCOM") || ex.Contains("UPCM") || ex.Contains("UPC")) return Upcom;
        return Hose; // HOSE / HSX / mặc định
    }

    public static (decimal Floor, decimal Ceiling) Calculate(decimal referencePrice, string? exchange)
    {
        if (referencePrice <= 0m)
            return (0m, 0m);

        var band = BandFor(exchange);
        var rawFloor = referencePrice * (1m - band);
        var rawCeiling = referencePrice * (1m + band);
        return (RoundToTick(rawFloor), RoundToTick(rawCeiling));
    }

    /// <summary>Làm tròn về bước giá HOSE (đvt: đồng). History lưu giá đồng.</summary>
    private static decimal RoundToTick(decimal price)
    {
        var tick = price switch
        {
            < 10_000m => 10m,
            < 50_000m => 50m,
            _ => 100m
        };
        return Math.Round(price / tick, MidpointRounding.AwayFromZero) * tick;
    }

    /// <summary>Proxy: đóng cửa ~ sàn và bằng Low → có khả năng bị chất sàn.</summary>
    public static bool IsLikelyFloorLocked(OhlcvBar bar, decimal? floorPrice)
    {
        if (floorPrice is null || floorPrice.Value <= 0m)
            return false;

        var tolerance = Math.Max(floorPrice.Value * 0.005m, 10m);
        return Math.Abs(bar.Close - floorPrice.Value) <= tolerance && bar.Close <= bar.Low + tolerance;
    }
}
