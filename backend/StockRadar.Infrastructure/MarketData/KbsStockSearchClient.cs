using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace StockRadar.Infrastructure.MarketData;

/// <summary>
/// Tên công ty từ KBS /stock/search/data (vnstock all_symbols).
/// </summary>
internal sealed class KbsStockSearchClient(
    HttpClient http,
    IMemoryCache cache,
    ILogger<KbsStockSearchClient> logger)
{
    private const string SearchUrl =
        "https://kbbuddywts.kbsec.com.vn/iis-server/investment/stock/search/data";

    private const string CacheKey = "kbs:stock-search-map";

    public async Task<IReadOnlyDictionary<string, string>> GetSymbolNamesAsync(
        CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, string>? cached) && cached is not null)
            return cached;

        var map = await BuildMapAsync(cancellationToken);
        cache.Set(CacheKey, map, TimeSpan.FromHours(12));
        return map;
    }

    public async Task<IReadOnlyList<string>> GetAllListedSymbolsAsync(
        CancellationToken cancellationToken = default)
    {
        var map = await GetSymbolNamesAsync(cancellationToken);
        return map.Keys.OrderBy(s => s).ToList();
    }

    private async Task<IReadOnlyDictionary<string, string>> BuildMapAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, SearchUrl);
            request.Headers.TryAddWithoutValidation("x-lang", "vi");
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("KBS stock/search HTTP {Status}", response.StatusCode);
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in array.EnumerateArray())
            {
                if (row.TryGetProperty("type", out var typeEl)
                    && typeEl.ValueKind == JsonValueKind.String
                    && !string.Equals(typeEl.GetString(), "stock", StringComparison.OrdinalIgnoreCase))
                    continue;

                var symbol = row.TryGetProperty("symbol", out var symEl) && symEl.ValueKind == JsonValueKind.String
                    ? symEl.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(symbol))
                    continue;

                var name = row.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                    ? nameEl.GetString()
                    : null;

                map[symbol.Trim().ToUpperInvariant()] = string.IsNullOrWhiteSpace(name)
                    ? symbol.Trim().ToUpperInvariant()
                    : name.Trim();
            }

            logger.LogInformation("KBS stock search: {Count} mã.", map.Count);
            return map;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Không lấy được danh sách tên mã KBS.");
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
