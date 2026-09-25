using StockRadar.Domain.Entities;

namespace StockRadar.Domain.Services;

public sealed record UniverseFilterSettings(
    decimal MinAvgDailyVolume,
    int VolumeLookbackSessions,
    int ExcludeIpoWithinDays,
    /// <summary>Giá đóng cửa tối thiểu (VND đầy đủ, ví dụ 8000 = 8.000đ).</summary>
    decimal MinClosePriceVnd = 8_000m,
    /// <summary>
    /// TB giá trị khớp tối thiểu (VND/phiên). Mã đạt nếu TB khối lượng (cp) HOẶC TB giá trị này đủ
    /// — tránh bỏ sót mã giá cao nhưng thanh khoản tốt. 0 = chỉ xét khối lượng (hành vi cũ).
    /// </summary>
    decimal MinAvgDailyValueVnd = 0m);

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

    /// <summary>
    /// Lịch sử có bị "đóng băng" (quá cũ) hoặc đứt đoạn (gap) trong <paramref name="lookback"/> phiên gần nhất không.
    /// Mã inactive không được append giá hàng ngày sẽ có nến cuối cũ và/hoặc một khoảng trống lớn
    /// (vd. đóng băng từ tháng 7). Không nên khôi phục những mã này vì chỉ báo kỹ thuật tính trên
    /// history đứt đoạn sẽ sai — cần Job 1 backfill lại đầy đủ. Ngưỡng <paramref name="maxGapCalendarDays"/>
    /// đủ rộng để bỏ qua nghỉ lễ/Tết bình thường nhưng bắt được gap do đóng băng.
    /// </summary>
    public static bool IsHistoryStale(
        IReadOnlyList<OhlcvBar> bars,
        DateOnly today,
        int lookback,
        int maxGapCalendarDays = 15)
    {
        if (bars.Count == 0)
            return true;

        var ordered = bars.OrderBy(b => b.Date).ToList();

        // Nến cuối quá cũ so với hôm nay → mã ngừng giao dịch / không được cập nhật.
        if (ordered[^1].Date.AddDays(maxGapCalendarDays) < today)
            return true;

        // Gap trong cửa sổ xét thanh khoản → lịch sử đứt đoạn (đóng băng), chỉ báo sẽ sai.
        var start = Math.Max(0, ordered.Count - Math.Max(1, lookback));
        for (var i = start + 1; i < ordered.Count; i++)
        {
            if (ordered[i].Date.DayNumber - ordered[i - 1].Date.DayNumber > maxGapCalendarDays)
                return true;
        }

        return false;
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
        var avgVol = IndicatorMath.AverageVolume(ordered, lookback);
        var avgVal = IndicatorMath.AverageTurnoverValue(ordered, lookback);

        // Đạt nếu TB khối lượng (cp) HOẶC TB giá trị khớp (VND) đủ — đo thanh khoản công bằng
        // giữa mã giá cao và giá thấp (chỉ số cp sẽ bỏ sót mã đắt nhưng khớp lệnh vài chục tỷ/phiên).
        var volumeOk = avgVol >= settings.MinAvgDailyVolume;
        var valueOk = settings.MinAvgDailyValueVnd > 0 && avgVal >= settings.MinAvgDailyValueVnd;
        if (!volumeOk && !valueOk)
        {
            return Fail(settings.MinAvgDailyValueVnd > 0
                ? $"TB KL {lookback} phiên {avgVol:N0} < {settings.MinAvgDailyVolume:N0} và TB GT {avgVal:N0} < {settings.MinAvgDailyValueVnd:N0}"
                : $"TB KL {lookback} phiên {avgVol:N0} < {settings.MinAvgDailyVolume:N0}");
        }

        return new UniverseScreenResult(true, "Đạt universe", Math.Round(avgVol, 0), ordered[0].Date);
    }

    private static UniverseScreenResult Fail(string reason) =>
        new(false, reason, 0, null);
}
