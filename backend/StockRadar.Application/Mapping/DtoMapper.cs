using StockRadar.Application.DTOs;
using StockRadar.Domain.Entities;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Mapping;

public static class DtoMapper
{    public static ScoreBreakdownDto ToDto(ScoreBreakdown breakdown) => new(
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

    public static FlatBoxDto? ToDto(
        FlatBoxProfile? profile,
        decimal maxGainFromBoxTopPercent)
    {
        if (profile is null || !profile.HasValidBox)
            return null;

        var gain = profile.GainFromBoxTopPercent;

        return new FlatBoxDto(
            profile.BoxLow,
            profile.BoxHigh,
            profile.SessionDays,
            profile.RefBoxPeriod,
            profile.IsBreakoutConfirmed,
            profile.PriceGainPercent,
            profile.VolumeMultiplier,
            profile.SuggestedStopLoss,
            gain,
            gain > maxGainFromBoxTopPercent,
            profile.BoxHigh,
            gain,
            [
                new BasePricePeriodDto(
                    profile.PeriodStart.ToString("yyyy-MM-dd"),
                    profile.PeriodEnd.ToString("yyyy-MM-dd"),
                    profile.SessionDays,
                    profile.BoxLow,
                    profile.BoxHigh)
            ]);
    }

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
            profile.QualityScore,
            profile.Quality is null
                ? null
                : new BaseQualityComponentsDto(
                    profile.Quality.PriorTrendScore,
                    profile.Quality.AtrContractionScore,
                    profile.Quality.CompressionScore,
                    profile.Quality.VolumeDryScore,
                    profile.Quality.ContractionPatternScore,
                    profile.Quality.DistributionScore,
                    profile.Quality.DurationScore,
                    profile.Quality.TotalScore),
            profile.Periods
                .Select(p => new BasePricePeriodDto(
                    p.FromDate.ToString("yyyy-MM-dd"),
                    p.ToDate.ToString("yyyy-MM-dd"),
                    p.SessionDays,
                    p.Low,
                    p.High))
                .ToList());
    }

    public static EntryPointDto ToDto(EntryPointEvaluation eval) => new(
        eval.Status.ToString(),
        eval.Type.ToString(),
        eval.Confidence,
        eval.EntryPrice,
        eval.StopLoss,
        eval.TriggerPrice,
        eval.TargetPrice,
        eval.BaseLow,
        eval.BaseHigh,
        eval.GainFromBasePercent,
        eval.RiskRewardRatio,
        eval.IsActionable,
        eval.Headline,
        eval.Action,
        eval.Checklist
            .Select(c => new EntryPointCheckDto(c.Id, c.Label, c.Passed, c.Detail))
            .ToList());

    public static BuyDecisionDto ToDto(BuyDecisionEvaluation decision, SwingDecisionDto? swing = null) => new(
        decision.BuyScore,
        decision.ActionScore,
        decision.Recommendation.ToString(),
        decision.PassesTopFilter,
        decision.GateFailure,
        decision.Reasons,
        decision.Breakdown
            .Select(c => new BuyScoreComponentDto(c.Id, c.Label, c.Points, c.MaxPoints, c.Detail))
            .ToList(),
        ToDto(decision.Entry),
        decision.PredictedHitPercent,
        decision.PredictedSampleCount,
        decision.SetupDna,
        decision.TopExplainLines,
        swing);
}