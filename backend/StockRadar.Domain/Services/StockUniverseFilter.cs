using StockRadar.Domain.Entities;

namespace StockRadar.Domain.Services;

public sealed record UniverseFilterSettings(
    decimal MinAvgDailyVolume,
    int VolumeLookbackSessions,
    int ExcludeIpoWithinDays,
    /// <summary>Giá đóng cửa tối thiểu (VND đầy đủ, ví dụ 8000 = 8.000đ).</summary>
    decimal MinClosePriceVnd = 8_000m);

public sealed record UniverseScreenResult(
    bool Passes,
    string Reason,
    decimal AvgVolume30d,
    DateOnly? FirstTradeDate);

public static class StockUniverseFilter
{
    public static UniverseScreenResult Screen(
        IReadOnlyList<OhlcvBar> screeningBars,
        bool tradingRestricted,
        string? tradingStatus,
        UniverseFilterSettings settings,
        DateOnly today)
    {
        if (tradingRestricted)
            return Fail($"Hạn chế giao dịch{(string.IsNullOrWhiteSpace(tradingStatus) ? "" : $": {tradingStatus}")}");

        if (screeningBars.Count == 0)
            return Fail("Không có dữ liệu giá");

        var ordered = screeningBars.OrderBy(b => b.Date).ToList();
        var firstTrade = ordered[0].Date;
        var ipoCutoff = today.AddDays(-settings.ExcludeIpoWithinDays);
        if (firstTrade >= ipoCutoff)
            return Fail($"IPO/niêm yết trong {settings.ExcludeIpoWithinDays} ngày ({firstTrade:dd/MM/yyyy})");

        return ScreenPriceAndVolume(ordered, settings);
    }

    /// <summary>Lọc chất lượng hàng ngày (giá + thanh khoản) — không kiểm IPO.</summary>
    public static UniverseScreenResult ScreenQuality(
        IReadOnlyList<OhlcvBar> bars,
        UniverseFilterSettings settings)
    {
        if (bars.Count == 0)
            return Fail("Không có dữ liệu giá");

        var ordered = bars.OrderBy(b => b.Date).ToList();
        return ScreenPriceAndVolume(ordered, settings);
    }

    private static UniverseScreenResult ScreenPriceAndVolume(
        IReadOnlyList<OhlcvBar> ordered,
        UniverseFilterSettings settings)
    {
        var latestClose = ordered[^1].Close;
        var minCloseStored = settings.MinClosePriceVnd / 1000m;
        if (latestClose <= minCloseStored)
        {
            var priceVnd = Math.Round(latestClose * 1000m, 0);
            return Fail($"Giá {priceVnd:N0} ≤ {settings.MinClosePriceVnd:N0}");
        }

        var lookback = Math.Min(settings.VolumeLookbackSessions, ordered.Count);
        var recent = ordered.TakeLast(lookback).ToList();
        var avgVol = recent.Average(b => (decimal)b.Volume);
        if (avgVol < settings.MinAvgDailyVolume)
            return Fail($"TB KL {lookback} phiên {avgVol:N0} < {settings.MinAvgDailyVolume:N0}");

        return new UniverseScreenResult(true, "Đạt universe", Math.Round(avgVol, 0), ordered[0].Date);
    }

    private static UniverseScreenResult Fail(string reason) =>
        new(false, reason, 0, null);
}
