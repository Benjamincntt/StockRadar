using StockRadar.Application.DTOs;
using StockRadar.Domain.Entities;

namespace StockRadar.Application.Mapping;

public static class DtoMapper
{
    public static MarketOverviewDto ToDto(MarketIndex index) => new(
        index.Symbol,
        index.Price,
        index.ChangePercent,
        index.Score,
        index.Trend);

    public static OhlcvBarDto ToDto(OhlcvBar bar) => new(
        bar.Date.ToString("yyyy-MM-dd"),
        bar.Open,
        bar.High,
        bar.Low,
        bar.Close,
        bar.Volume);
}
