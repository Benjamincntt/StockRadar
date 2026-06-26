using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace StockRadar.Infrastructure.MarketData;

internal sealed record KbsStockListing(
    string Symbol,
    string Name,
    string Exchange,
    bool TradingRestricted,
    string? TradingStatus);

/// <summary>Metadata niêm yết từ KBS /stock/search/data.</summary>
internal sealed class KbsStockListingClient(
    HttpClient http,
    IMemoryCache cache,
    ILogger<KbsStockListingClient> logger)
{
    private const string SearchUrl =
        "https://kbbuddywts.kbsec.com.vn/iis-server/investment/stock/search/data";

    private const string CacheKey = "kbs:stock-listings";

    public async Task<IReadOnlyDictionary<string, KbsStockListing>> GetListingsAsync(
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        if (!bypassCache
            && cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, KbsStockListing>? cached)
            && cached is not null)
            return cached;

        var map = await BuildMapAsync(cancellationToken);
        if (!bypassCache)
            cache.Set(CacheKey, map, TimeSpan.FromHours(12));
        return map;
    }

    public async Task<KbsStockListing?> GetListingAsync(
        string symbol,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        var map = await GetListingsAsync(bypassCache, cancellationToken);
        map.TryGetValue(symbol.Trim().ToUpperInvariant(), out var listing);
        return listing;
    }

    private async Task<IReadOnlyDictionary<string, KbsStockListing>> BuildMapAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, SearchUrl);
            request.Headers.TryAddWithoutValidation("x-lang", "vi");
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("KBS stock/search HTTP {Status}", response.StatusCode);
                return new Dictionary<string, KbsStockListing>(StringComparer.OrdinalIgnoreCase);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var array = doc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => doc.RootElement,
                JsonValueKind.Object when doc.RootElement.TryGetProperty("data", out var data)
                    && data.ValueKind == JsonValueKind.Array => data,
                _ => default
            };

            if (array.ValueKind != JsonValueKind.Array)
                return new Dictionary<string, KbsStockListing>(StringComparer.OrdinalIgnoreCase);

            var map = new Dictionary<string, KbsStockListing>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in array.EnumerateArray())
            {
                if (row.TryGetProperty("type", out var typeEl)
                    && typeEl.ValueKind == JsonValueKind.String
                    && !string.Equals(typeEl.GetString(), "stock", StringComparison.OrdinalIgnoreCase))
                    continue;

                var symbol = GetString(row, "symbol", "sb", "code");
                if (string.IsNullOrWhiteSpace(symbol))
                    continue;

                var name = GetString(row, "name", "companyName") ?? symbol;
                var exchange = NormalizeExchange(GetString(row, "exchange", "floor", "market", "board"));
                var statusRaw = GetString(row, "status", "tradingStatus", "state", "note") ?? "";
                var restricted = IsTradingRestricted(statusRaw, row);

                map[symbol.Trim().ToUpperInvariant()] = new KbsStockListing(
                    symbol.Trim().ToUpperInvariant(),
                    name.Trim(),
                    exchange,
                    restricted,
                    string.IsNullOrWhiteSpace(statusRaw) ? null : statusRaw.Trim());
            }

            logger.LogInformation("KBS listings: {Count} mã.", map.Count);
            return map;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Không lấy được metadata niêm yết KBS.");
            return new Dictionary<string, KbsStockListing>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static bool IsTradingRestricted(string statusRaw, JsonElement row)
    {
        if (string.IsNullOrWhiteSpace(statusRaw))
            return false;

        var s = statusRaw.ToUpperInvariant();
        if (s.Contains("HALT", StringComparison.Ordinal)
            || s.Contains("SUSPEND", StringComparison.Ordinal)
            || s.Contains("CẢNH BÁO", StringComparison.OrdinalIgnoreCase)
            || s.Contains("HẠN CHẾ", StringComparison.OrdinalIgnoreCase)
            || s.Contains("KIỂM SOÁT", StringComparison.OrdinalIgnoreCase)
            || s.Contains("ĐẶC BIỆT", StringComparison.OrdinalIgnoreCase)
            || s.Contains("RESTRICT", StringComparison.Ordinal))
            return true;

        if (row.TryGetProperty("tradingRestricted", out var flag)
            && flag.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return flag.GetBoolean();

        return false;
    }

    private static string NormalizeExchange(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var v = raw.Trim().ToUpperInvariant();
        return v switch
        {
            "HSX" or "HOSE" => "HOSE",
            "HNX" => "HNX",
            "UPCOM" or "UPC" => "UPCOM",
            _ => v
        };
    }

    private static string? GetString(JsonElement row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.TryGetProperty(name, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }

        return null;
    }
}
