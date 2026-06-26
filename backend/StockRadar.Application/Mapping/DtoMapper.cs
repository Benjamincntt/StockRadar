using StockRadar.Application.DTOs;
using StockRadar.Domain.Entities;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Mapping;

public static class DtoMapper
{
    public static ScoreBreakdownDto ToDto(ScoreBreakdown breakdown) => new(
        breakdown.MarketTrend,
        breakdown.SectorStrength,
        breakdown.RelativeStrength,
        breakdown.Accumulation,
        breakdown.Breakout,
        breakdown.VolumeExpansion);

    public static MarketOverviewDto ToDto(MarketIndex index) => new(
        index.Symbol,
        index.Price,
        index.ChangePercent,
        index.Score,
        index.Trend);

    public static SectorDto ToDto(SectorScore sector) => new(sector.Name, sector.Score, sector.ChangePercent);

    public static OpportunityDto ToDto(Stock stock, StockScore score, DateTime? generatedAt = null) => new(
        stock.Symbol,
        stock.Name,
        score.Total,
        stock.LatestPrice,
        score.ChangePercent,
        score.VolumeRatio,
        stock.Sector,
        generatedAt);

    public static RadarItemDto ToRadarDto(
        Stock stock,
        StockScore score,
        IReadOnlyList<Domain.Enums.SignalType> signals) => new(
        stock.Symbol,
        stock.Name,
        stock.Sector,
        score.Total,
        stock.LatestPrice,
        score.ChangePercent,
        score.VolumeRatio,
        score.RelativeStrength,
        signals);

    public static AlertDto ToDto(
        Alert alert,
        bool inOpportunity = false,
        bool inWatchlist = false) => new(
        alert.Id,
        alert.Symbol,
        alert.Type,
        alert.Title,
        alert.Message,
        alert.CreatedAt,
        alert.Category,
        alert.VolumeRatio,
        alert.RelativeStrength,
        alert.SectorRank,
        inOpportunity,
        inWatchlist);

    public static OhlcvBarDto ToDto(OhlcvBar bar) => new(
        bar.Date.ToString("yyyy-MM-dd"),
        bar.Open,
        bar.High,
        bar.Low,
        bar.Close,
        bar.Volume);

    public static BasePriceDto? ToDto(
        BasePriceProfile? profile,
        BasePriceProfile? filterProfile,
        decimal maxGainFromBasePercent)
    {
        if (profile is null)
            return null;

        var filterGain = filterProfile?.GainFromBasePercent ?? 0;
        var filterHigh = filterProfile?.BaseHigh ?? 0;

        return new BasePriceDto(
            profile.BaseLow,
            profile.BaseHigh,
            profile.TotalSessionDays,
            profile.GainFromBasePercent,
            profile.BaseIndex,
            profile.TotalBases,
            filterHigh,
            filterGain,
            filterGain > maxGainFromBasePercent,
            profile.Periods
                .Select(p => new BasePricePeriodDto(
                    p.FromDate.ToString("yyyy-MM-dd"),
                    p.ToDate.ToString("yyyy-MM-dd"),
                    p.SessionDays,
                    p.Low,
                    p.High))
                .ToList());
    }
}
