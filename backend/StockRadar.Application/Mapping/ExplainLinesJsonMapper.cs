using System.Text.Json;

namespace StockRadar.Application.Mapping;

public static class ExplainLinesJsonMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string? ToJson(IReadOnlyList<string>? lines)
    {
        if (lines is null || lines.Count == 0)
            return null;

        return JsonSerializer.Serialize(lines, JsonOptions);
    }

    public static IReadOnlyList<string>? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
