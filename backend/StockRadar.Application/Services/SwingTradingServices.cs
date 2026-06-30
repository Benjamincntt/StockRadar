using Microsoft.Extensions.Options;
using StockRadar.Application.Abstractions;
using StockRadar.Application.Common;
using StockRadar.Application.DTOs;
using StockRadar.Application.Options;
using StockRadar.Domain.Enums;
using StockRadar.Domain.Services;

namespace StockRadar.Application.Services;

public interface ISwingDecisionService
{
    Task<SwingDecisionDto> BuildAsync(
        BuyDecisionEvaluation decision,
        SmartMoneyMarketContext context,
        string symbol,
        CancellationToken cancellationToken = default);
}

public sealed class SwingDecisionService(
    ISetupTrackRepository tracks,
    IEntryTimingRepository entryTiming,
    ITradeJournalRepository journal,
    ICurrentUserService currentUser,
    IOptions<SwingTradingOptions> swingOptions) : ISwingDecisionService
{
    public async Task<SwingDecisionDto> BuildAsync(
        BuyDecisionEvaluation decision,
        SmartMoneyMarketContext context,
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var cfg = swingOptions.Value;
        var today = TradingCalendar.TodayVietnam();
        var from7d = TradingSessionMath.SubtractTradingSessions(today, 7);
        var (measured7d, good7d) = await tracks.GetMeasuredOpportunityCountsSinceAsync(from7d, cancellationToken);
        decimal? winRate7d = measured7d > 0
            ? Math.Round(100m * good7d / measured7d, 1)
            : null;

        var personal = await journal.GetCalibrationAsync(currentUser.UserId, cancellationToken);
        var personalFactor = personal?.Factor ?? 1m;

        var regime = RegimeOverlayEvaluator.Apply(
            decision.PredictedHitPercent,
            new RegimeOverlayInput(
                context.MarketPhase,
                winRate7d,
                measured7d,
                personalFactor),
            cfg.LowWinRateThreshold);

        EntryTimingHint? timingHint = null;
        var timingState = await entryTiming.GetAsync(cancellationToken);
        if (timingState is not null)
        {
            timingHint = EntryTimingAnalyzer.BuildHint(new EntryTimingStats(
                timingState.TopOnlyMeasured,
                timingState.TopOnlyGood,
                timingState.ConfirmMeasured,
                timingState.ConfirmGood));
        }

        var hadConfirm = await tracks.HasMasterConfirmAsync(
            symbol,
            today,
            cancellationToken);

        var result = SwingDecisionEngine.Evaluate(new SwingDecisionInput(
            decision,
            regime,
            timingHint,
            cfg.RiskPercentPerTrade,
            cfg.MaxPositionPercent,
            hadConfirm));

        return new SwingDecisionDto(
            result.Verdict.ToString(),
            result.Headline,
            result.Detail,
            result.AdjustedHitPercent,
            decision.PredictedHitPercent,
            result.SuggestedSizePercent,
            result.RiskRewardRatio,
            result.RegimeSizeFactor,
            result.RequiresMasterConfirm,
            regime.Notes.ToList(),
            result.Reasons.ToList(),
            personalFactor,
            winRate7d,
            measured7d);
    }
}

public interface ITradeJournalService
{
    Task<TradeJournalEntryDto> AddAsync(
        CreateTradeJournalRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TradeJournalEntryDto>> GetRecentAsync(
        int limit = 30,
        CancellationToken cancellationToken = default);

    Task<PersonalCalibrationDto?> GetPersonalCalibrationAsync(
        CancellationToken cancellationToken = default);
}

public sealed class TradeJournalService(
    ITradeJournalRepository journal,
    ISetupTrackRepository tracks,
    ICurrentUserService currentUser,
    IOptions<SwingTradingOptions> swingOptions) : ITradeJournalService
{
    public async Task<TradeJournalEntryDto> AddAsync(
        CreateTradeJournalRequest request,
        CancellationToken cancellationToken = default)
    {
        var entry = new TradeJournalRecord(
            Guid.NewGuid(),
            currentUser.UserId,
            request.Symbol.Trim().ToUpperInvariant(),
            request.TradeDate ?? TradingCalendar.TodayVietnam(),
            request.Action,
            request.SizePercent,
            request.EngineVerdict,
            request.Note,
            request.BuyScore,
            request.PredictedHit,
            request.SetupDna,
            DateTime.UtcNow);

        await journal.AddAsync(entry, cancellationToken);
        await RebuildPersonalCalibrationAsync(cancellationToken);
        return ToDto(entry);
    }

    public async Task<IReadOnlyList<TradeJournalEntryDto>> GetRecentAsync(
        int limit = 30,
        CancellationToken cancellationToken = default)
    {
        var rows = await journal.GetForUserAsync(currentUser.UserId, limit, cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<PersonalCalibrationDto?> GetPersonalCalibrationAsync(
        CancellationToken cancellationToken = default)
    {
        var cal = await journal.GetCalibrationAsync(currentUser.UserId, cancellationToken);
        return cal is null
            ? null
            : new PersonalCalibrationDto(cal.Factor, cal.SampleCount, cal.UpdatedAt);
    }

    private async Task RebuildPersonalCalibrationAsync(CancellationToken cancellationToken)
    {
        var cfg = swingOptions.Value;
        var entries = await journal.GetForUserAsync(currentUser.UserId, 100, cancellationToken);
        if (entries.Count < 3)
            return;

        var measured = await tracks.GetMeasuredOpportunitySetupsAsync(cancellationToken);
        var outcomeByKey = measured
            .Where(m => m.PredictedHitPercent is > 0)
            .GroupBy(m => (m.Symbol, m.EntryDate))
            .ToDictionary(g => g.Key, g => g.First());

        decimal weightedPredicted = 0;
        decimal weightedActual = 0;
        decimal weightSum = 0;
        var samples = 0;

        foreach (var e in entries.Where(x => x.Action == "Entered"))
        {
            if (!outcomeByKey.TryGetValue((e.Symbol, e.TradeDate), out var track))
                continue;
            if (track.OutcomeBucket is null || e.PredictedHit is null or <= 0)
                continue;

            var ageWeeks = TradingSessionMath.TradingSessionsBetween(e.TradeDate, TradingCalendar.TodayVietnam()) / 5m;
            var decay = (decimal)Math.Pow((double)cfg.JournalDecayFactor, (double)Math.Max(0, ageWeeks));
            var actual = track.OutcomeBucket == "Good" ? 100m : track.OutcomeBucket == "Flat" ? 50m : 0m;

            weightedPredicted += e.PredictedHit.Value * decay;
            weightedActual += actual * decay;
            weightSum += decay;
            samples++;
        }

        if (samples < 3 || weightSum <= 0)
            return;

        var avgPredicted = weightedPredicted / weightSum;
        var avgActual = weightedActual / weightSum;
        var factor = avgPredicted > 0
            ? Math.Clamp(avgActual / avgPredicted, 0.75m, 1.25m)
            : 1m;

        await journal.SaveCalibrationAsync(
            currentUser.UserId,
            new PersonalCalibrationRecord(Math.Round(factor, 3), samples, DateTime.UtcNow),
            cancellationToken);
    }

    private static TradeJournalEntryDto ToDto(TradeJournalRecord e) => new(
        e.Id,
        e.Symbol,
        e.TradeDate,
        e.Action,
        e.SizePercent,
        e.EngineVerdict,
        e.Note,
        e.BuyScore,
        e.PredictedHit,
        e.SetupDna,
        e.CreatedAt);
}

public sealed class EntryTimingService(
    ISetupTrackRepository tracks,
    IEntryTimingRepository entryTiming)
{
    public async Task RebuildAsync(CancellationToken cancellationToken = default)
    {
        var rows = await tracks.GetMeasuredOpportunitiesForEntryTimingAsync(cancellationToken);
        if (rows.Count == 0)
            return;

        var stats = EntryTimingAnalyzer.Aggregate(
            rows.Select(r => (r.HadMasterConfirm == true, r.OutcomeBucket)));
        var hint = EntryTimingAnalyzer.BuildHint(stats);
        await entryTiming.SaveAsync(
            new EntryTimingStateRecord(
                stats.TopOnlyMeasured,
                stats.TopOnlyGood,
                stats.ConfirmMeasured,
                stats.ConfirmGood,
                hint?.PreferMasterConfirm ?? false,
                DateTime.UtcNow),
            cancellationToken);
    }
}
