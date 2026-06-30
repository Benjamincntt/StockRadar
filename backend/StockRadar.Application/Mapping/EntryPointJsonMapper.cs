using System.Text.Json;
using StockRadar.Application.DTOs;

namespace StockRadar.Application.Mapping;

public static class EntryPointJsonMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string? ToJson(EntryPointDto? dto)
    {
        if (dto is null)
            return null;

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public static EntryPointDto? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<EntryPointDto>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
