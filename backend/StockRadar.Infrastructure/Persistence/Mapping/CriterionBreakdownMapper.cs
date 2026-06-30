using System.Text.Json;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Infrastructure.Persistence.Mapping;

internal static class CriterionBreakdownMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    internal sealed record Payload(
        List<BucketDto> Buckets,
        List<PhaseDto> Phases);

    internal sealed record BucketDto(string Id, int HitCount, int TotalCount);

    internal sealed record PhaseDto(string Phase, int HitCount, int TotalCount);

    public static string ToJson(CriterionAccuracySnapshot snapshot)
    {
        var payload = new Payload(
            snapshot.Buckets?.Select(b => new BucketDto(b.BucketId, b.HitCount, b.TotalCount)).ToList() ?? [],
            snapshot.Phases?.Select(p => new PhaseDto(p.Phase.ToString(), p.HitCount, p.TotalCount)).ToList() ?? []);
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static (IReadOnlyList<CriterionScoreBucketStats> Buckets, IReadOnlyList<CriterionPhaseStats> Phases) FromJson(
        string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ([], []);

        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(json, JsonOptions);
            if (payload is null)
                return ([], []);

            var buckets = payload.Buckets
                .Select(b => new CriterionScoreBucketStats(
                    b.Id,
                    b.HitCount,
                    b.TotalCount,
                    b.TotalCount > 0 ? Math.Round((decimal)b.HitCount / b.TotalCount * 100m, 1) : 0m))
                .ToList();

            var phases = payload.Phases
                .Select(p => new CriterionPhaseStats(
                    Enum.TryParse<MarketWyckoffPhase>(p.Phase, out var phase) ? phase : MarketWyckoffPhase.Neutral,
                    p.HitCount,
                    p.TotalCount,
                    p.TotalCount > 0 ? Math.Round((decimal)p.HitCount / p.TotalCount * 100m, 1) : 0m))
                .ToList();

            return (buckets, phases);
        }
        catch
        {
            return ([], []);
        }
    }

    public static IReadOnlyList<CriterionScoreBucketStats> MergeBuckets(
        IEnumerable<IReadOnlyList<CriterionScoreBucketStats>> sources)
    {
        var map = new Dictionary<string, (int Hits, int Total)>(StringComparer.Ordinal);
        foreach (var list in sources)
        {
            foreach (var b in list)
            {
                map.TryGetValue(b.BucketId, out var cur);
                map[b.BucketId] = (cur.Hits + b.HitCount, cur.Total + b.TotalCount);
            }
        }

        return map
            .OrderBy(kv => kv.Key)
            .Select(kv => new CriterionScoreBucketStats(
                kv.Key,
                kv.Value.Hits,
                kv.Value.Total,
                kv.Value.Total > 0 ? Math.Round((decimal)kv.Value.Hits / kv.Value.Total * 100m, 1) : 0m))
            .ToList();
    }

    public static IReadOnlyList<CriterionPhaseStats> MergePhases(
        IEnumerable<IReadOnlyList<CriterionPhaseStats>> sources)
    {
        var map = new Dictionary<MarketWyckoffPhase, (int Hits, int Total)>();
        foreach (var list in sources)
        {
            foreach (var p in list)
            {
                map.TryGetValue(p.Phase, out var cur);
                map[p.Phase] = (cur.Hits + p.HitCount, cur.Total + p.TotalCount);
            }
        }

        return map
            .OrderBy(kv => kv.Key)
            .Select(kv => new CriterionPhaseStats(
                kv.Key,
                kv.Value.Hits,
                kv.Value.Total,
                kv.Value.Total > 0 ? Math.Round((decimal)kv.Value.Hits / kv.Value.Total * 100m, 1) : 0m))
            .ToList();
    }
}
