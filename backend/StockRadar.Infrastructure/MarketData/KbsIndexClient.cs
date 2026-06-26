using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// VNINDEX từ KBS index API (stock/iss không có giá chỉ số).
/// </summary>
internal sealed class KbsIndexClient(
    HttpClient http,
    IMemoryCache cache,
    ILogger<KbsIndexClient> logger)
{
    private const string BaseUrl = "https://kbbuddywts.kbsec.com.vn/iis-server/investment/index/VNINDEX";

    public async Task<KbsIndexSnapshot?> FetchVnIndexAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue("kbs:vnindex", out KbsIndexSnapshot? cached) && cached is not null)
            return cached;

        var today = TodayVietnam();
        var start = today.AddDays(-7);

        var intraday = await FetchBarsAsync("data_5P", today, today, cancellationToken);
        var daily = await FetchBarsAsync("data_day", start, today, cancellationToken);

        if (intraday.Count == 0 && daily.Count == 0)
        {
            logger.LogWarning("KBS không trả dữ liệu VNINDEX.");
            return null;
        }

        var snapshot = BuildSnapshot(intraday, daily);
        if (snapshot is null)
            return null;

        cache.Set("kbs:vnindex", snapshot, TimeSpan.FromSeconds(30));
        return snapshot;
    }

    private static KbsIndexSnapshot? BuildSnapshot(
        IReadOnlyList<KbsOhlcBar> intraday,
        IReadOnlyList<KbsOhlcBar> daily)
    {
        if (daily.Count == 0 && intraday.Count == 0)
            return null;

        var sortedDaily = daily.OrderBy(b => b.Time).ToList();
        var latestDaily = sortedDaily[^1];
        var prevDaily = sortedDaily.Count >= 2 ? sortedDaily[^2] : latestDaily;

        decimal price;
        decimal reference;

        if (intraday.Count > 0)
        {
            var sortedIntra = intraday.OrderBy(b => b.Time).ToList();
            price = sortedIntra[^1].Close;
            reference = sortedIntra[0].Open > 0 ? sortedIntra[0].Open : prevDaily.Close;
            if (reference <= 0)
                reference = prevDaily.Close;
        }
        else
        {
            price = latestDaily.Close;
            reference = prevDaily.Close;
        }

        if (price <= 0)
            return null;

        var changePercent = reference > 0
            ? Math.Round((price - reference) / reference * 100m, 2)
            : 0m;

        return new KbsIndexSnapshot(price, changePercent);
    }

    private async Task<IReadOnlyList<KbsOhlcBar>> FetchBarsAsync(
        string suffix,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        var url =
            $"{BaseUrl}/{suffix}?sdate={FormatKbsDate(start)}&edate={FormatKbsDate(end)}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("x-lang", "vi");
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return [];

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var key = suffix;
            if (!doc.RootElement.TryGetProperty(key, out var array) || array.ValueKind != JsonValueKind.Array)
                return [];

            return array.EnumerateArray()
                .Select(ParseBar)
                .Where(b => b is not null)
                .Cast<KbsOhlcBar>()
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Không lấy được VNINDEX {Suffix}.", suffix);
            return [];
        }
    }

    private static KbsOhlcBar? ParseBar(JsonElement row)
    {
        var time = ParseTime(row);
        if (time is null)
            return null;

        var close = GetDecimal(row, "c", "close");
        if (close <= 0)
            return null;

        var open = GetDecimal(row, "o", "open");
        if (open <= 0) open = close;

        return new KbsOhlcBar(time.Value, open, close);
    }

    private static DateTimeOffset? ParseTime(JsonElement row)
    {
        if (!row.TryGetProperty("t", out var tProp))
            return null;

        if (tProp.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(
                tProp.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var parsed))
            return parsed;

        return null;
    }

    private static decimal GetDecimal(JsonElement row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.TryGetProperty(name, out var value))
                continue;

            return value.ValueKind switch
            {
                JsonValueKind.Number => value.GetDecimal(),
                JsonValueKind.String when decimal.TryParse(
                    value.GetString(),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var parsed) => parsed,
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

    internal sealed record KbsOhlcBar(DateTimeOffset Time, decimal Open, decimal Close);

    internal sealed record KbsIndexSnapshot(decimal Price, decimal ChangePercent);
}
