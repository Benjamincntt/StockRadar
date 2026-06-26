using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Ngành từ KBS listing API (cùng nguồn vnstock Listing.symbols_by_industries).
/// </summary>
internal sealed class KbsSectorLookupClient(
    HttpClient http,
    IMemoryCache cache,
    ILogger<KbsSectorLookupClient> logger)
{
    private const string SectorAllUrl =
        "https://kbbuddywts.kbsec.com.vn/iis-server/investment/sector/all";

    private const string SectorStockUrl =
        "https://kbbuddywts.kbsec.com.vn/iis-server/investment/sector/stock";

    private const string CacheKey = "kbs:symbol-sectors";

    public Task<IReadOnlyDictionary<string, string>> GetSymbolSectorsAsync(
        CancellationToken cancellationToken = default,
        bool bypassCache = false) =>
        GetSymbolSectorsCoreAsync(bypassCache, cancellationToken);

    private async Task<IReadOnlyDictionary<string, string>> GetSymbolSectorsCoreAsync(
        bool bypassCache,
        CancellationToken cancellationToken)
    {
        if (!bypassCache
            && cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, string>? cached)
            && cached is not null)
            return cached;

        var map = await BuildMapAsync(cancellationToken);
        if (!bypassCache)
            cache.Set(CacheKey, map, TimeSpan.FromHours(12));
        return map;
    }

    public async Task<string?> ResolveAsync(string symbol, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return null;

        var map = await GetSymbolSectorsAsync(cancellationToken);
        return map.TryGetValue(symbol.Trim().ToUpperInvariant(), out var sector) ? sector : null;
    }

    private async Task<IReadOnlyDictionary<string, string>> BuildMapAsync(CancellationToken cancellationToken)
    {
        var industries = await FetchIndustriesAsync(cancellationToken);
        if (industries.Count == 0)
        {
            logger.LogWarning("KBS sector/all không trả dữ liệu.");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var map = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var industry in industries)
        {
            if (industry.Code <= 0 || string.IsNullOrWhiteSpace(industry.Name))
                continue;

            var symbols = await FetchSymbolsByIndustryAsync(industry.Code, cancellationToken);
            foreach (var symbol in symbols)
                map[symbol] = industry.Name;
        }

        logger.LogInformation("KBS sector map: {Count} mã, {Industries} ngành.", map.Count, industries.Count);
        return map.ToDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<KbsIndustry>> FetchIndustriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, SectorAllUrl);
            request.Headers.TryAddWithoutValidation("x-lang", "vi");
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return [];

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var root = doc.RootElement;
            var array = root.ValueKind switch
            {
                JsonValueKind.Array => root,
                JsonValueKind.Object when root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
                    => data,
                _ => default
            };

            if (array.ValueKind != JsonValueKind.Array)
                return [];

            return array.EnumerateArray()
                .Select(ParseIndustry)
                .Where(i => i is not null)
                .Cast<KbsIndustry>()
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Không lấy được danh sách ngành KBS.");
            return [];
        }
    }

    private async Task<IReadOnlyList<string>> FetchSymbolsByIndustryAsync(
        int industryCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{SectorStockUrl}?code={industryCode}&l=1";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("x-lang", "vi");
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return [];

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
                return ParseSymbolArray(root);

            if (root.TryGetProperty("stocks", out var stocks) && stocks.ValueKind == JsonValueKind.Array)
            {
                return stocks.EnumerateArray()
                    .Select(s => s.TryGetProperty("sb", out var sb) ? sb.GetString() : null)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!.Trim().ToUpperInvariant())
                    .ToList();
            }

            if (root.TryGetProperty("data", out var data))
                return data.ValueKind == JsonValueKind.Array ? ParseSymbolArray(data) : [];

            return [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Không lấy được mã ngành KBS {Code}.", industryCode);
            return [];
        }
    }

    private static IReadOnlyList<string> ParseSymbolArray(JsonElement array) =>
        array.EnumerateArray()
            .Select(el => el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Object when el.TryGetProperty("sb", out var sb) => sb.GetString(),
                _ => null
            })
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim().ToUpperInvariant())
            .ToList();

    private static KbsIndustry? ParseIndustry(JsonElement row)
    {
        var code = row.TryGetProperty("code", out var codeEl) ? codeEl.GetInt32() : 0;
        var name = row.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
            ? nameEl.GetString()
            : null;

        if (code <= 0 || string.IsNullOrWhiteSpace(name))
            return null;

        return new KbsIndustry(code, name.Trim());
    }

    private sealed record KbsIndustry(int Code, string Name);
}
