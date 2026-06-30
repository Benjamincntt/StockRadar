using System.Text.Json;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Mapping;

public static class BuyScoreBreakdownMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed record ItemDto(string Id, string Label, int Points, int MaxPoints, string Detail);

    public static string ToJson(IReadOnlyList<BuyScoreComponent> breakdown)
    {
        if (breakdown.Count == 0)
            return "[]";

        var items = breakdown.Select(c => new ItemDto(c.Id, c.Label, c.Points, c.MaxPoints, c.Detail)).ToList();
        return JsonSerializer.Serialize(items, JsonOptions);
    }

    public static IReadOnlyList<BuyScoreComponent> FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]")
            return [];

        try
        {
            var items = JsonSerializer.Deserialize<List<ItemDto>>(json, JsonOptions);
            return items?.Select(i => new BuyScoreComponent(i.Id, i.Label, i.Points, i.MaxPoints, i.Detail)).ToList()
                ?? [];
        }
        catch
        {
            return [];
        }
    }
}
