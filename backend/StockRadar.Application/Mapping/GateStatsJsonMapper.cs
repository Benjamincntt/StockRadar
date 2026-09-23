using System.Text.Json;

namespace StockRadar.Application.Mapping;

/// <summary>Gate rejection stats (nhãn gate → số mã bị loại) ↔ JSON lưu cùng DailyAnalysisRun.</summary>
public static class GateStatsJsonMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string? ToJson(IReadOnlyDictionary<string, int>? stats)
    {
        if (stats is null || stats.Count == 0)
            return null;

        return JsonSerializer.Serialize(stats, JsonOptions);
    }

    public static IReadOnlyDictionary<string, int>? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var stats = JsonSerializer.Deserialize<Dictionary<string, int>>(json, JsonOptions);
            return stats is { Count: > 0 } ? stats : null;
        }
        catch
        {
            return null;
        }
    }
}
