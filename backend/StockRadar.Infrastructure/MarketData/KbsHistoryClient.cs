using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace StockRadar.Infrastructure.MarketData;

internal sealed class KbsHistoryClient(
    HttpClient http,
    IMemoryCache cache,
    ILogger<KbsHistoryClient> logger)
{
    private const string BaseUrl = "https://kbbuddywts.kbsec.com.vn/iis-server/investment";
    private const decimal PriceScale = 1000m;

    private static readonly IReadOnlyDictionary<string, string> IntervalSuffix =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["1m"] = "1P",
            ["5m"] = "5P",
            ["15m"] = "15P",
            ["30m"] = "30P",
            ["1H"] = "60P",
            ["1h"] = "60P",
            ["1D"] = "day",
            ["1d"] = "day",
        };

    private static readonly IReadOnlyDictionary<string, int> DefaultBarCount =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["1m"] = 300,
            ["5m"] = 240,
            ["15m"] = 200,
            ["30m"] = 200,
            ["1H"] = 200,
            ["1D"] = 120,
        };

    public static bool IsSupported(string interval) =>
        IntervalSuffix.ContainsKey(interval);

    public async Task<IReadOnlyList<KbsChartBar>> FetchAsync(
        string symbol,
        string interval,
        CancellationToken cancellationToken = default)
    {
        if (!IntervalSuffix.TryGetValue(interval, out var suffix))
            throw new ArgumentException($"Interval không hỗ trợ: {interval}");

        var sym = symbol.Trim().ToUpperInvariant();
        var cacheKey = $"kbs:chart:{sym}:{interval.ToUpperInvariant()}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<KbsChartBar>? cached) && cached is not null)
            return cached;

        var barCount = DefaultBarCount.TryGetValue(interval, out var count) ? count : 200;
        var (start, end) = GetDateRange(interval, barCount);
        var bars = await FetchRangeAsync(sym, suffix, start, end, barCount, cancellationToken);

        var ttl = interval.Equals("1D", StringComparison.OrdinalIgnoreCase)
            ? TimeSpan.FromMinutes(5)
            : TimeSpan.FromSeconds(45);

        cache.Set(cacheKey, bars, ttl);
        return bars;
    }

    /// <summary>Lịch sử ngày đầy đủ theo khoảng thời gian (không cache — dùng backfill).</summary>
    public Task<IReadOnlyList<KbsChartBar>> FetchDailyHistoryAsync(
        string symbol,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default)
    {
        var sym = symbol.Trim().ToUpperInvariant();
        var spanDays = Math.Max(1, end.DayNumber - start.DayNumber + 1);
        return FetchRangeAsync(sym, "day", start, end, spanDays + 30, cancellationToken);
    }

    private async Task<IReadOnlyList<KbsChartBar>> FetchRangeAsync(
        string symbol,
        string suffix,
        DateOnly start,
        DateOnly end,
        int maxBars,
        CancellationToken cancellationToken)
    {
        var url =
            $"{BaseUrl}/stocks/{symbol}/data_{suffix}?sdate={FormatKbsDate(start)}&edate={FormatKbsDate(end)}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("x-lang", "vi");
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("KBS chart HTTP {Status} {Symbol} {Suffix}", response.StatusCode, symbol, suffix);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var dataKey = $"data_{suffix}";
            if (!doc.RootElement.TryGetProperty(dataKey, out var data) || data.ValueKind != JsonValueKind.Array)
            {
                logger.LogWarning("KBS chart thiếu key {Key} cho {Symbol}", dataKey, symbol);
                return [];
            }

            return data.EnumerateArray()
                .Select(ParseBar)
                .Where(b => b is not null)
                .Cast<KbsChartBar>()
                .OrderBy(b => b.Time)
                .TakeLast(maxBars)
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Không lấy được chart KBS {Symbol} {Suffix}", symbol, suffix);
            return [];
        }
    }

    private static KbsChartBar? ParseBar(JsonElement row)
    {
        var time = ParseTime(row);
        if (time is null)
            return null;

        var open = ScalePrice(GetDecimal(row, "o", "open"));
        var high = ScalePrice(GetDecimal(row, "h", "high"));
        var low = ScalePrice(GetDecimal(row, "l", "low"));
        var close = ScalePrice(GetDecimal(row, "c", "close"));
        if (close <= 0)
            return null;

        if (open <= 0) open = close;
        if (high <= 0) high = Math.Max(open, close);
        if (low <= 0) low = Math.Min(open, close);

        var volume = (long)GetDecimal(row, "v", "volume");
        return new KbsChartBar(time.Value, open, high, low, close, volume);
    }

    private static DateTimeOffset? ParseTime(JsonElement row)
    {
        if (row.TryGetProperty("t", out var tProp) || row.TryGetProperty("time", out tProp))
        {
            return tProp.ValueKind switch
            {
                JsonValueKind.String => DateTimeOffset.TryParse(
                    tProp.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var parsed)
                    ? parsed
                    : null,
                JsonValueKind.Number => DateTimeOffset.FromUnixTimeMilliseconds((long)tProp.GetDouble()),
                _ => null
            };
        }

        return null;
    }

    private static decimal ScalePrice(decimal raw) =>
        raw >= 1000m ? raw / PriceScale : raw;

    private static decimal GetDecimal(JsonElement row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.TryGetProperty(name, out var value))
                continue;

            return value.ValueKind switch
            {
                JsonValueKind.Number => value.GetDecimal(),
                JsonValueKind.String when decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                    => parsed,
                _ => 0m
            };
        }

        return 0m;
    }

    private static string FormatKbsDate(DateOnly date) =>
        date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    private static DateOnly TodayVietnam()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
    }

    private static (DateOnly Start, DateOnly End) GetDateRange(string interval, int barCount)
    {
        var end = TodayVietnam();
        var extraDays = interval.ToUpperInvariant() switch
        {
            "1D" => barCount + 10,
            "1H" => barCount / 5 + 15,
            "30M" => barCount / 10 + 12,
            "15M" => barCount / 16 + 10,
            "5M" => barCount / 48 + 8,
            "1M" => barCount / 240 + 5,
            _ => 30
        };

        return (end.AddDays(-Math.Max(extraDays, 7)), end);
    }

    internal sealed record KbsChartBar(
        DateTimeOffset Time,
        decimal Open,
        decimal High,
        decimal Low,
        decimal Close,
        long Volume);
}
