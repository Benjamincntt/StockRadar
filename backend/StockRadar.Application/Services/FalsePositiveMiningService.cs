using StockRadar.Application.Abstractions;
using StockRadar.Application.Mapping;
using StockRadar.Domain.Services;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Services;

public sealed class FalsePositiveMiningService(
    ISetupTrackRepository tracks,
    ICriterionScoringRepository criterionRepo,
    IFalsePositiveMiningRepository miningRepo)
{
    public async Task<FalsePositiveMiningResult> RunAndApplyAsync(CancellationToken cancellationToken = default)
    {
        var rows = await tracks.GetMeasuredOpportunitySetupsAsync(cancellationToken);
        var samples = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.ScoreBreakdownJson))
            .Select(r => new SetupOutcomeBreakdownSample(
                r.PredictedHitPercent ?? 0,
                r.OpportunityScore ?? 0,
                r.OutcomeBucket ?? "",
                BuyScoreBreakdownMapper.FromJson(r.ScoreBreakdownJson)))
            .ToList();

        var result = FalsePositiveAnalyzer.Analyze(samples);
        await miningRepo.SaveAsync(result, cancellationToken);

        if (!result.HasActionablePenalties)
            return result;

        var weights = await criterionRepo.GetWeightDetailsAsync(cancellationToken);
        if (weights.Count == 0)
            return result;

        var penaltyMap = result.Penalties.ToDictionary(p => p.CriterionType, p => p.WeightPenalty);
        var adjusted = weights
            .Select(w => penaltyMap.TryGetValue(w.Type, out var penalty)
                ? CriterionReviewHelper.ApplyFalsePositivePenalty(w, penalty)
                : w)
            .ToList();

        await criterionRepo.UpsertWeightsAsync(adjusted, cancellationToken);
        return result;
    }
}
