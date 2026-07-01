using StockRadar.Application.Abstractions;
using StockRadar.Application.DTOs;
using StockRadar.Infrastructure.MarketData;

namespace StockRadar.Infrastructure.MarketData;

internal sealed class StockLookupService(
    KbsStockSearchClient kbs,
    IStockRepository stocks) : IStockLookupService
{
    public async Task<IReadOnlyList<StockSearchHitDto>> SearchAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var q = query.Trim();
        if (q.Length < 1)
            return [];

        limit = Math.Clamp(limit, 1, 30);
        var map = await kbs.GetSymbolNamesAsync(cancellationToken);
        if (map.Count == 0)
            map = await BuildMapFromDbAsync(cancellationToken);

        var upper = q.ToUpperInvariant();
        var results = new List<StockSearchHitDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (map.TryGetValue(upper, out var exactName))
        {
            results.Add(new StockSearchHitDto(upper, exactName));
            seen.Add(upper);
        }

        foreach (var (symbol, name) in map.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (results.Count >= limit)
                break;
            if (!seen.Add(symbol))
                continue;

            if (symbol.Contains(upper, StringComparison.OrdinalIgnoreCase)
                || name.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new StockSearchHitDto(symbol, name));
            }
        }

        return results;
    }

    private async Task<IReadOnlyDictionary<string, string>> BuildMapFromDbAsync(
        CancellationToken cancellationToken)
    {
        var all = await stocks.GetAllAsync(cancellationToken);
        return all.ToDictionary(
            s => s.Symbol,
            s => string.IsNullOrWhiteSpace(s.Name) ? s.Symbol : s.Name,
            StringComparer.OrdinalIgnoreCase);
    }
}
