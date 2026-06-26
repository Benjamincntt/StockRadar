using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Danh sách mã theo chỉ số KBS (cùng nguồn vnstock Listing.symbols_by_group).
/// GET /index/{groupCode}/stocks
/// </summary>
internal sealed class KbsGroupListingClient(
    HttpClient http,
    IMemoryCache cache,
    ILogger<KbsGroupListingClient> logger)
{
    private const string IndexBaseUrl =
        "https://kbbuddywts.kbsec.com.vn/iis-server/investment/index";

    private static readonly IReadOnlyDictionary<string, string> GroupCodes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VN100"] = "100",
            ["VN30"] = "30",
            ["VNMidCap"] = "MID",
            ["VNMID"] = "MID",
            ["VNSmallCap"] = "SML",
            ["VNSI"] = "SI",
            ["VNX50"] = "X50",
            ["VNXALL"] = "XALL",
            ["VNALL"] = "ALL",
            ["HNX30"] = "HNX30",
            ["HOSE"] = "HOSE",
            ["HNX"] = "HNX",
            ["UPCOM"] = "UPCOM",
        };

    public async Task<IReadOnlyList<string>> GetSymbolsByGroupAsync(
        string group,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        if (!GroupCodes.TryGetValue(group.Trim(), out var code))
            code = group.Trim();

        var cacheKey = $"kbs:group:{code}";
        if (!bypassCache
            && cache.TryGetValue(cacheKey, out IReadOnlyList<string>? cached)
            && cached is not null)
            return cached;

        var url = $"{IndexBaseUrl}/{code}/stocks";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("x-lang", "vi");
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("KBS group HTTP {Status} {Group}", response.StatusCode, group);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var symbols = ParseSymbols(doc.RootElement);
            if (!bypassCache)
                cache.Set(cacheKey, symbols, TimeSpan.FromHours(6));
            logger.LogInformation("KBS nhóm {Group} ({Code}): {Count} mã.", group, code, symbols.Count);
            return symbols;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Không lấy được danh sách nhóm KBS {Group}.", group);
            return [];
        }
    }

    public async Task<IReadOnlyList<string>> GetUnionSymbolsAsync(
        IEnumerable<string> groups,
        bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            var symbols = await GetSymbolsByGroupAsync(group, bypassCache, cancellationToken);
            foreach (var symbol in symbols)
                set.Add(symbol);
        }

        return set.OrderBy(s => s).ToList();
    }

    private static IReadOnlyList<string> ParseSymbols(JsonElement root)
    {
        JsonElement array = root.ValueKind switch
        {
            JsonValueKind.Array => root,
            JsonValueKind.Object when root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
                => data,
            _ => default
        };

        if (array.ValueKind != JsonValueKind.Array)
            return [];

        return array.EnumerateArray()
            .Select(el => el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Object when el.TryGetProperty("symbol", out var sym) => sym.GetString(),
                JsonValueKind.Object when el.TryGetProperty("sb", out var sb) => sb.GetString(),
                _ => null
            })
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();
    }
}
