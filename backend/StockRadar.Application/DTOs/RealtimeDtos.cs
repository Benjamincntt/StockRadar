namespace StockRadar.Application.DTOs;

public sealed record QuoteTickDto(
    string Symbol,
    decimal Price,
    decimal ChangePercent,
    long Volume,
    DateTime UpdatedAt);

public sealed record IndexTickDto(
    string Symbol,
    decimal Price,
    decimal ChangePercent,
    int MarketScore,
    string Trend,
    DateTime UpdatedAt);

public sealed record SparklineDto(
    string Symbol,
    IReadOnlyList<decimal> Prices,
    decimal Reference);
