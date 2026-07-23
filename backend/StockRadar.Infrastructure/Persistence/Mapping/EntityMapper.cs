using System.Text.Json;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Infrastructure.Persistence.Entities;

namespace StockRadar.Infrastructure.Persistence.Mapping;

internal static class EntityMapper
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static Stock ToDomain(StockEntity entity)
    {
        var history = JsonSerializer.Deserialize<List<OhlcvBar>>(entity.HistoryJson, JsonOptions) ?? [];
        return new Stock(
            entity.Symbol,
            entity.Name,
            string.IsNullOrWhiteSpace(entity.Sector) ? "" : entity.Sector,
            history,
            entity.LastChangePercent,
            entity.IsActive,
            entity.Exchange,
            entity.TradingRestricted,
            entity.SectorLocked);
    }

    public static StockEntity ToEntity(Stock stock) => new()
    {
        Symbol = stock.Symbol,
        Name = stock.Name,
        Sector = stock.Sector,
        SectorLocked = stock.SectorLocked,
        HistoryJson = JsonSerializer.Serialize(stock.History, JsonOptions),
        LastChangePercent = stock.LastChangePercent,
        IsActive = stock.IsActive,
        Exchange = stock.Exchange,
        TradingRestricted = stock.TradingRestricted
    };

    public static Alert ToDomain(AlertEntity entity) => new(
        entity.Id,
        entity.Symbol,
        (SignalType)entity.Type,
        entity.Title,
        entity.Message,
        DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc),
        (AlertCategory)entity.Category,
        entity.VolumeRatio,
        entity.RelativeStrength,
        entity.SectorRank);

    public static AlertEntity ToEntity(Alert alert) => new()
    {
        Id = alert.Id,
        Symbol = alert.Symbol,
        Type = (int)alert.Type,
        Title = alert.Title,
        Message = alert.Message,
        CreatedAt = alert.CreatedAt,
        Category = (int)alert.Category,
        VolumeRatio = alert.VolumeRatio,
        RelativeStrength = alert.RelativeStrength,
        SectorRank = alert.SectorRank
    };

    public static MarketIndex ToDomain(MarketIndexEntity entity)
    {
        var history = JsonSerializer.Deserialize<List<OhlcvBar>>(entity.HistoryJson, JsonOptions) ?? [];
        var change5d = ComputeChangePercent(history, 5, entity.ChangePercent);
        return new MarketIndex(
            entity.Symbol,
            entity.Price,
            entity.ChangePercent,
            entity.Score,
            (MarketTrend)entity.Trend,
            change5d,
            history);
    }

    public static MarketIndexEntity ToEntity(MarketIndex index, string historyJson) => new()
    {
        Symbol = index.Symbol,
        Price = index.Price,
        ChangePercent = index.ChangePercent,
        Score = index.Score,
        Trend = (int)index.Trend,
        UpdatedAt = DateTime.UtcNow,
        HistoryJson = historyJson
    };

    public static decimal ComputeChangePercent(IReadOnlyList<OhlcvBar> history, int days, decimal fallback)
    {
        if (history.Count < days + 1)
            return fallback;

        var latest = history[^1].Close;
        var previous = history[^(days + 1)].Close;
        if (previous <= 0)
            return fallback;

        return Math.Round((latest - previous) / previous * 100m, 2);
    }

    public static MarketIndexEntity ToEntity(MarketIndex index) => new()
    {
        Symbol = index.Symbol,
        Price = index.Price,
        ChangePercent = index.ChangePercent,
        Score = index.Score,
        Trend = (int)index.Trend,
        UpdatedAt = DateTime.UtcNow,
        HistoryJson = "[]"
    };

    public static UserAccount ToDomain(UserEntity entity) =>
        new(entity.Id, entity.Email, entity.PasswordHash, entity.DisplayName);
}
