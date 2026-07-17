using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.MasterAlerts;
using StockRadar.Infrastructure.Persistence.Entities;

namespace StockRadar.Infrastructure.Persistence.Repositories;

internal sealed class EfSetupTrackRepository(ApplicationDbContext db) : ISetupTrackRepository
{
    public async Task AddAsync(SetupTrackRecord track, CancellationToken cancellationToken = default)
    {
        db.SetupTracks.Add(ToEntity(track));
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(
        string symbol,
        string sourceType,
        DateOnly entryDate,
        CancellationToken cancellationToken = default) =>
        db.SetupTracks.AnyAsync(
            x => x.Symbol == symbol
                && x.SourceType == sourceType
                && x.EntryDate == entryDate,
            cancellationToken);

    public async Task<IReadOnlyList<SetupTrackRecord>> GetPendingOutcomesAsync(
        DateOnly measureThroughDate,
        CancellationToken cancellationToken = default) =>
        await db.SetupTracks
            .Where(x => !x.OutcomeMeasured && x.EntryDate <= measureThroughDate)
            .OrderBy(x => x.EntryDate)
            .Select(x => ToRecord(x))
            .ToListAsync(cancellationToken);

    public async Task UpdateOutcomeAsync(
        Guid id,
        decimal forwardPriceT25,
        decimal forwardReturnPercent,
        string outcomeBucket,
        DateOnly weekStart,
        bool? hadMasterConfirm,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.SetupTracks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return;

        entity.OutcomeMeasured = true;
        entity.ForwardPriceT25 = forwardPriceT25;
        entity.ForwardReturnPercent = forwardReturnPercent;
        entity.OutcomeBucket = outcomeBucket;
        entity.MeasuredAt = DateTime.UtcNow;
        entity.WeekStartDate = weekStart;
        if (hadMasterConfirm is not null)
            entity.HadMasterConfirm = hadMasterConfirm;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SetupTrackRecord>> GetPendingSwingMetricsAsync(
        DateOnly measureThroughDate,
        CancellationToken cancellationToken = default) =>
        await db.SetupTracks
            .Where(x => x.OutcomeMeasured
                && !x.SwingMetricsMeasured
                && x.EntryDate <= measureThroughDate
                && x.SourceType == MasterAlertKinds.Opportunity)
            .OrderBy(x => x.EntryDate)
            .Select(x => ToRecord(x))
            .ToListAsync(cancellationToken);

    public async Task UpdateSwingMetricsAsync(
        Guid id,
        decimal? forwardReturnT5,
        decimal? forwardReturnT10,
        string? outcomeBucketT5,
        string? outcomeBucketT10,
        decimal maxFavorableExcursionPercent,
        decimal maxAdverseExcursionPercent,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.SetupTracks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return;

        entity.ForwardReturnT5 = forwardReturnT5;
        entity.ForwardReturnT10 = forwardReturnT10;
        entity.OutcomeBucketT5 = outcomeBucketT5;
        entity.OutcomeBucketT10 = outcomeBucketT10;
        entity.MaxFavorableExcursionPercent = maxFavorableExcursionPercent;
        entity.MaxAdverseExcursionPercent = maxAdverseExcursionPercent;
        entity.SwingMetricsMeasured = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> HasMasterConfirmAsync(
        string symbol,
        DateOnly entryDate,
        CancellationToken cancellationToken = default) =>
        db.SetupTracks.AnyAsync(
            x => x.Symbol == symbol
                && x.EntryDate == entryDate
                && x.SourceType == MasterAlertKinds.BuyPoint1,
            cancellationToken);

    public async Task<IReadOnlyList<SetupTrackRecord>> GetMeasuredOpportunitiesForEntryTimingAsync(
        CancellationToken cancellationToken = default) =>
        await db.SetupTracks
            .Where(x => x.OutcomeMeasured
                && x.SourceType == MasterAlertKinds.Opportunity
                && x.HadMasterConfirm != null)
            .Select(x => ToRecord(x))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SetupTrackRecord>> GetForWeekAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken = default) =>
        await db.SetupTracks
            .Where(x => x.WeekStartDate == weekStart && x.OutcomeMeasured)
            .OrderByDescending(x => x.EntryDate)
            .Select(x => ToRecord(x))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<string, SetupTrackRecord>> GetOpportunityMapForDateAsync(
        DateOnly forTradingDate,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.SetupTracks.AsNoTracking()
            .Where(x => x.SourceType == MasterAlertKinds.Opportunity && x.EntryDate == forTradingDate)
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(x => x.Symbol, ToRecord, StringComparer.OrdinalIgnoreCase);
    }

    public async Task RegisterOpportunitiesAsync(
        DateOnly forTradingDate,
        IReadOnlyList<OpportunityTrackSeed> seeds,
        CancellationToken cancellationToken = default)
    {
        foreach (var seed in seeds)
        {
            var existing = await db.SetupTracks.FirstOrDefaultAsync(
                x => x.Symbol == seed.Symbol
                    && x.SourceType == MasterAlertKinds.Opportunity
                    && x.EntryDate == forTradingDate,
                cancellationToken);

            if (existing is not null)
            {
                existing.OpportunityRank = seed.Rank;
                existing.OpportunityScore = seed.Score;
                existing.EntryPrice = seed.Price;
                existing.SessionChangePercent = seed.ChangePercent;
                existing.PredictedHitPercent = seed.PredictedHitPercent > 0 ? seed.PredictedHitPercent : existing.PredictedHitPercent;
                existing.SetupDna = seed.SetupDna ?? existing.SetupDna;
                if (!string.IsNullOrWhiteSpace(seed.ScoreBreakdownJson))
                    existing.ScoreBreakdownJson = seed.ScoreBreakdownJson;
                if (!string.IsNullOrWhiteSpace(seed.TradeState))
                    existing.TradeState = seed.TradeState;
                if (!string.IsNullOrWhiteSpace(seed.TradeStateReason))
                    existing.TradeStateReason = seed.TradeStateReason;
                continue;
            }

            db.SetupTracks.Add(new SetupTrackEntity
            {
                Id = Guid.NewGuid(),
                Symbol = seed.Symbol,
                SourceType = MasterAlertKinds.Opportunity,
                EntryDate = forTradingDate,
                EntryPrice = seed.Price,
                OpportunityForDate = forTradingDate,
                OpportunityRank = seed.Rank,
                OpportunityScore = seed.Score,
                SessionChangePercent = seed.ChangePercent,
                PredictedHitPercent = seed.PredictedHitPercent > 0 ? seed.PredictedHitPercent : null,
                SetupDna = seed.SetupDna,
                ScoreBreakdownJson = seed.ScoreBreakdownJson,
                TradeState = seed.TradeState,
                TradeStateReason = seed.TradeStateReason,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SetupTrackRecord>> GetMeasuredWithPredictionsAsync(
        CancellationToken cancellationToken = default) =>
        await db.SetupTracks
            .Where(x => x.OutcomeMeasured && x.PredictedHitPercent != null && x.PredictedHitPercent > 0)
            .OrderByDescending(x => x.MeasuredAt)
            .Select(x => ToRecord(x))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SetupTrackRecord>> GetMeasuredOpportunitySetupsAsync(
        CancellationToken cancellationToken = default) =>
        await db.SetupTracks
            .Where(x => x.OutcomeMeasured && x.SourceType == MasterAlertKinds.Opportunity)
            .OrderByDescending(x => x.MeasuredAt)
            .Select(x => ToRecord(x))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SetupTrackRecord>> GetMeasuredOpportunitiesSinceAsync(
        DateOnly fromEntryDate,
        CancellationToken cancellationToken = default) =>
        await db.SetupTracks
            .Where(x => x.OutcomeMeasured
                && x.SourceType == MasterAlertKinds.Opportunity
                && x.EntryDate >= fromEntryDate)
            .OrderByDescending(x => x.EntryDate)
            .ThenBy(x => x.OpportunityRank)
            .Select(x => ToRecord(x))
            .ToListAsync(cancellationToken);

    public async Task<(int Measured, int Good)> GetMeasuredOpportunityCountsSinceAsync(
        DateOnly fromEntryDate,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.SetupTracks
            .Where(x => x.OutcomeMeasured
                && x.SourceType == MasterAlertKinds.Opportunity
                && x.EntryDate >= fromEntryDate)
            .Select(x => x.OutcomeBucket)
            .ToListAsync(cancellationToken);

        return (rows.Count, rows.Count(b => b == OutcomeBucketNames.Good));
    }

    public async Task<AlertHistoryPage> GetAlertHistoryAsync(
        int limit,
        int skip,
        bool? outcomeMeasured,
        string? sourceType,
        bool buyPointsOnly,
        DateOnly? fromEntryDate = null,
        DateOnly? toEntryDateInclusive = null,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        skip = Math.Max(0, skip);

        var opp = MasterAlertKinds.Opportunity;
        var buy1 = MasterAlertKinds.BuyPoint1;
        var buy2 = MasterAlertKinds.BuyPoint2;

        IQueryable<SetupTrackEntity> Scope(IQueryable<SetupTrackEntity> q) =>
            buyPointsOnly
                ? q.Where(x => x.SourceType == buy1 || x.SourceType == buy2)
                : q.Where(x => x.SourceType == opp || x.SourceType == buy1 || x.SourceType == buy2);

        IQueryable<SetupTrackEntity> ApplyDateFilter(IQueryable<SetupTrackEntity> q)
        {
            if (fromEntryDate is not null)
                q = q.Where(x => x.EntryDate >= fromEntryDate.Value);
            if (toEntryDateInclusive is not null)
                q = q.Where(x => x.EntryDate <= toEntryDateInclusive.Value);
            return q;
        }

        var query = ApplyDateFilter(Scope(db.SetupTracks.AsNoTracking()));

        if (!string.IsNullOrWhiteSpace(sourceType))
            query = query.Where(x => x.SourceType == sourceType);

        if (outcomeMeasured is not null)
            query = query.Where(x => x.OutcomeMeasured == outcomeMeasured.Value);

        var totalTracked = await query.CountAsync(cancellationToken);

        var aggregateQuery = ApplyDateFilter(Scope(db.SetupTracks.AsNoTracking()));
        if (!string.IsNullOrWhiteSpace(sourceType))
            aggregateQuery = aggregateQuery.Where(x => x.SourceType == sourceType);

        var totalPending = await aggregateQuery.Where(x => !x.OutcomeMeasured).CountAsync(cancellationToken);
        var measuredRows = await aggregateQuery
            .Where(x => x.OutcomeMeasured)
            .Select(x => x.OutcomeBucket)
            .ToListAsync(cancellationToken);

        var totalSuccess = measuredRows.Count(b => b == OutcomeBucketNames.Good);
        var totalFailed = measuredRows.Count(b => b == OutcomeBucketNames.Failed);
        var totalFlat = measuredRows.Count(b => b == OutcomeBucketNames.Flat);
        var totalMeasured = totalSuccess + totalFailed + totalFlat;

        var alerts = await query
            .OrderByDescending(x => x.EntryDate)
            .ThenBy(x => x.Symbol)
            .Skip(skip)
            .Take(limit)
            .Select(x => ToRecord(x))
            .ToListAsync(cancellationToken);

        return new AlertHistoryPage(
            totalTracked,
            totalMeasured,
            totalSuccess,
            totalFailed,
            totalFlat,
            totalPending,
            alerts);
    }

    public async Task<IReadOnlyList<SetupTrackRecord>> GetAlertHistoryTracksAsync(
        bool buyPointsOnly,
        string? sourceType,
        CancellationToken cancellationToken = default)
    {
        var opp = MasterAlertKinds.Opportunity;
        var buy1 = MasterAlertKinds.BuyPoint1;
        var buy2 = MasterAlertKinds.BuyPoint2;

        var query = buyPointsOnly
            ? db.SetupTracks.AsNoTracking().Where(x => x.SourceType == buy1 || x.SourceType == buy2)
            : db.SetupTracks.AsNoTracking()
                .Where(x => x.SourceType == opp || x.SourceType == buy1 || x.SourceType == buy2);

        if (!string.IsNullOrWhiteSpace(sourceType))
            query = query.Where(x => x.SourceType == sourceType);

        return await query
            .OrderByDescending(x => x.EntryDate)
            .ThenBy(x => x.Symbol)
            .Select(x => ToRecord(x))
            .ToListAsync(cancellationToken);
    }

    private static SetupTrackRecord ToRecord(SetupTrackEntity e) => new(
        e.Id,
        e.Symbol,
        e.SourceType,
        e.EntryDate,
        e.EntryPrice,
        e.OpportunityForDate,
        e.OpportunityRank,
        e.OpportunityScore,
        e.SessionChangePercent,
        e.SessionVolume,
        e.PeakGainPercent,
        e.OutcomeMeasured,
        e.ForwardPriceT25,
        e.ForwardReturnPercent,
        e.OutcomeBucket,
        e.MeasuredAt,
        e.WeekStartDate,
        e.PredictedHitPercent,
        e.SetupDna,
        e.ScoreBreakdownJson,
        e.ForwardReturnT5,
        e.ForwardReturnT10,
        e.OutcomeBucketT5,
        e.OutcomeBucketT10,
        e.MaxFavorableExcursionPercent,
        e.MaxAdverseExcursionPercent,
        e.SwingMetricsMeasured,
        e.HadMasterConfirm,
        e.TradeState,
        e.TradeStateReason);

    private static SetupTrackEntity ToEntity(SetupTrackRecord r) => new()
    {
        Id = r.Id,
        Symbol = r.Symbol,
        SourceType = r.SourceType,
        EntryDate = r.EntryDate,
        EntryPrice = r.EntryPrice,
        OpportunityForDate = r.OpportunityForDate,
        OpportunityRank = r.OpportunityRank,
        OpportunityScore = r.OpportunityScore,
        SessionChangePercent = r.SessionChangePercent,
        SessionVolume = r.SessionVolume,
        PeakGainPercent = r.PeakGainPercent,
        OutcomeMeasured = r.OutcomeMeasured,
        ForwardPriceT25 = r.ForwardPriceT25,
        ForwardReturnPercent = r.ForwardReturnPercent,
        OutcomeBucket = r.OutcomeBucket,
        MeasuredAt = r.MeasuredAt,
        WeekStartDate = r.WeekStartDate,
        PredictedHitPercent = r.PredictedHitPercent,
        SetupDna = r.SetupDna,
        ScoreBreakdownJson = r.ScoreBreakdownJson,
        ForwardReturnT5 = r.ForwardReturnT5,
        ForwardReturnT10 = r.ForwardReturnT10,
        OutcomeBucketT5 = r.OutcomeBucketT5,
        OutcomeBucketT10 = r.OutcomeBucketT10,
        MaxFavorableExcursionPercent = r.MaxFavorableExcursionPercent,
        MaxAdverseExcursionPercent = r.MaxAdverseExcursionPercent,
        SwingMetricsMeasured = r.SwingMetricsMeasured,
        HadMasterConfirm = r.HadMasterConfirm,
        TradeState = r.TradeState,
        TradeStateReason = r.TradeStateReason,
    };
}

internal sealed class EfMasterAlertPositionRepository(ApplicationDbContext db) : IMasterAlertPositionRepository
{
    public async Task<IReadOnlyList<MasterAlertPositionRecord>> GetOpenPositionsAsync(
        CancellationToken ct = default)
    {
        var rows = await db.MasterAlertPositions
            .AsNoTracking()
            .Where(x => !x.IsClosed)
            .ToListAsync(ct);
        return rows.Select(ToRecord).ToList();
    }

    public async Task<MasterAlertPositionRecord?> GetOpenBySymbolAsync(
        string symbol,
        CancellationToken ct = default)
    {
        var key = symbol.Trim().ToUpperInvariant();
        var entity = await db.MasterAlertPositions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => !x.IsClosed && x.Symbol == key, ct);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task UpsertOnBuyAsync(
        string symbol,
        DateOnly entryDate,
        decimal entryPrice,
        decimal positionSize,
        string firedKind,
        string? marketPhase,
        CancellationToken ct = default)
    {
        var key = symbol.Trim().ToUpperInvariant();
        var existing = await db.MasterAlertPositions
            .FirstOrDefaultAsync(x => !x.IsClosed && x.Symbol == key, ct);
        var now = DateTime.UtcNow;

        if (existing is null)
        {
            var kinds = new List<string> { firedKind };
            db.MasterAlertPositions.Add(new MasterAlertPositionEntity
            {
                Id = Guid.NewGuid(),
                Symbol = key,
                EntryDate = entryDate,
                EntryPrice = entryPrice,
                PeakPriceSinceEntry = Math.Max(entryPrice, 0m),
                CurrentPositionSize = positionSize,
                FiredAlertKindsJson = SerializeKinds(kinds),
                MarketPhaseAtEntry = marketPhase,
                IsClosed = false,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else
        {
            existing.CurrentPositionSize = Math.Max(existing.CurrentPositionSize, positionSize);
            existing.PeakPriceSinceEntry = Math.Max(existing.PeakPriceSinceEntry, entryPrice);
            existing.FiredAlertKindsJson = AppendKind(existing.FiredAlertKindsJson, firedKind);
            if (string.IsNullOrWhiteSpace(existing.MarketPhaseAtEntry) && !string.IsNullOrWhiteSpace(marketPhase))
                existing.MarketPhaseAtEntry = marketPhase;
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(
        Guid id,
        decimal peakPrice,
        decimal positionSize,
        string? appendFiredKind,
        CancellationToken ct = default)
    {
        var entity = await db.MasterAlertPositions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return;

        entity.PeakPriceSinceEntry = peakPrice;
        entity.CurrentPositionSize = positionSize;
        if (!string.IsNullOrWhiteSpace(appendFiredKind))
            entity.FiredAlertKindsJson = AppendKind(entity.FiredAlertKindsJson, appendFiredKind);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task CloseAsync(
        Guid id,
        DateOnly closedDate,
        string appendFiredKind,
        CancellationToken ct = default)
    {
        var entity = await db.MasterAlertPositions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
            return;

        entity.CurrentPositionSize = 0m;
        entity.IsClosed = true;
        entity.ClosedDate = closedDate;
        entity.FiredAlertKindsJson = AppendKind(entity.FiredAlertKindsJson, appendFiredKind);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static MasterAlertPositionRecord ToRecord(MasterAlertPositionEntity e) => new(
        e.Id,
        e.Symbol,
        e.EntryDate,
        e.EntryPrice,
        e.PeakPriceSinceEntry,
        e.CurrentPositionSize,
        DeserializeKinds(e.FiredAlertKindsJson),
        e.MarketPhaseAtEntry,
        e.IsClosed,
        e.ClosedDate);

    private static IReadOnlyList<string> DeserializeKinds(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string SerializeKinds(IReadOnlyList<string> kinds) =>
        System.Text.Json.JsonSerializer.Serialize(kinds);

    private static string AppendKind(string json, string kind)
    {
        var list = DeserializeKinds(json).ToList();
        if (!list.Contains(kind, StringComparer.Ordinal))
            list.Add(kind);
        return SerializeKinds(list);
    }
}

internal sealed class EfWeeklyOpportunityReviewRepository(ApplicationDbContext db)
    : IWeeklyOpportunityReviewRepository
{
    public async Task UpsertAsync(WeeklyOpportunityReviewRecord review, CancellationToken cancellationToken = default)
    {
        var existing = await db.WeeklyOpportunityReviews
            .FirstOrDefaultAsync(x => x.WeekStartDate == review.WeekStartDate, cancellationToken);
        if (existing is null)
        {
            db.WeeklyOpportunityReviews.Add(ToEntity(review));
        }
        else
        {
            existing.TotalTracked = review.TotalTracked;
            existing.MeasuredCount = review.MeasuredCount;
            existing.GoodCount = review.GoodCount;
            existing.FlatCount = review.FlatCount;
            existing.FailedCount = review.FailedCount;
            existing.SuccessRatePercent = review.SuccessRatePercent;
            existing.FailedRatePercent = review.FailedRatePercent;
            existing.OpportunityCount = review.OpportunityCount;
            existing.BuyPoint1Count = review.BuyPoint1Count;
            existing.BuyPoint2Count = review.BuyPoint2Count;
            existing.CutLoss1Count = review.CutLoss1Count;
            existing.CutAllCount = review.CutAllCount;
            existing.OpportunitySuccessRate = review.OpportunitySuccessRate;
            existing.BuyPoint1SuccessRate = review.BuyPoint1SuccessRate;
            existing.BuyPoint2SuccessRate = review.BuyPoint2SuccessRate;
            existing.RecommendedAction = review.RecommendedAction;
            existing.Summary = review.Summary;
            existing.GeneratedAt = review.GeneratedAt;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<WeeklyOpportunityReviewRecord?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        var entity = await db.WeeklyOpportunityReviews
            .OrderByDescending(x => x.WeekStartDate)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<WeeklyOpportunityReviewRecord?> GetForWeekAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.WeeklyOpportunityReviews
            .FirstOrDefaultAsync(x => x.WeekStartDate == weekStart, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    private static WeeklyOpportunityReviewRecord ToRecord(WeeklyOpportunityReviewEntity e) => new(
        e.WeekStartDate,
        e.TotalTracked,
        e.MeasuredCount,
        e.GoodCount,
        e.FlatCount,
        e.FailedCount,
        e.SuccessRatePercent,
        e.FailedRatePercent,
        e.OpportunityCount,
        e.BuyPoint1Count,
        e.BuyPoint2Count,
        e.CutLoss1Count,
        e.CutAllCount,
        e.OpportunitySuccessRate,
        e.BuyPoint1SuccessRate,
        e.BuyPoint2SuccessRate,
        e.RecommendedAction,
        e.Summary,
        e.GeneratedAt);

    private static WeeklyOpportunityReviewEntity ToEntity(WeeklyOpportunityReviewRecord r) => new()
    {
        WeekStartDate = r.WeekStartDate,
        TotalTracked = r.TotalTracked,
        MeasuredCount = r.MeasuredCount,
        GoodCount = r.GoodCount,
        FlatCount = r.FlatCount,
        FailedCount = r.FailedCount,
        SuccessRatePercent = r.SuccessRatePercent,
        FailedRatePercent = r.FailedRatePercent,
        OpportunityCount = r.OpportunityCount,
        BuyPoint1Count = r.BuyPoint1Count,
        BuyPoint2Count = r.BuyPoint2Count,
        CutLoss1Count = r.CutLoss1Count,
        CutAllCount = r.CutAllCount,
        OpportunitySuccessRate = r.OpportunitySuccessRate,
        BuyPoint1SuccessRate = r.BuyPoint1SuccessRate,
        BuyPoint2SuccessRate = r.BuyPoint2SuccessRate,
        RecommendedAction = r.RecommendedAction,
        Summary = r.Summary,
        GeneratedAt = r.GeneratedAt,
    };
}

internal static class OutcomeBucketNames
{
    public const string Good = "Good";
    public const string Flat = "Flat";
    public const string Failed = "Failed";
}

internal sealed class EfShadowAnalysisRepository(ApplicationDbContext db) : IShadowAnalysisRepository
{
    public async Task ReplacePicksForVariantAsync(
        DateOnly forTradingDate,
        int variantMinPassScore,
        IReadOnlyList<ShadowPickSeed> picks,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.ShadowPicks
            .Where(x => x.ForTradingDate == forTradingDate && x.VariantMinPassScore == variantMinPassScore)
            .ToListAsync(cancellationToken);
        db.ShadowPicks.RemoveRange(existing);

        foreach (var pick in picks)
        {
            db.ShadowPicks.Add(new ShadowPickEntity
            {
                Id = Guid.NewGuid(),
                ForTradingDate = forTradingDate,
                VariantMinPassScore = variantMinPassScore,
                Symbol = pick.Symbol,
                Rank = pick.Rank,
                Score = pick.Score,
                EntryPrice = pick.Price,
                PredictedHitPercent = pick.PredictedHitPercent,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShadowPickRecord>> GetPendingOutcomesAsync(
        DateOnly measureThroughDate,
        CancellationToken cancellationToken = default) =>
        await db.ShadowPicks
            .Where(x => !x.OutcomeMeasured && x.ForTradingDate <= measureThroughDate)
            .OrderBy(x => x.ForTradingDate)
            .Select(x => ToRecord(x))
            .ToListAsync(cancellationToken);

    public async Task UpdateOutcomeAsync(
        Guid id,
        decimal forwardReturnPercent,
        string outcomeBucket,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.ShadowPicks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return;

        entity.OutcomeMeasured = true;
        entity.ForwardReturnPercent = forwardReturnPercent;
        entity.OutcomeBucket = outcomeBucket;
        entity.MeasuredAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShadowVariantSummaryRecord>> GetSummariesAsync(
        CancellationToken cancellationToken = default) =>
        await db.ShadowVariantSummaries
            .OrderBy(x => x.VariantMinPassScore)
            .Select(x => ToRecord(x))
            .ToListAsync(cancellationToken);

    public async Task RebuildSummariesAsync(
        int productionMinPassScore,
        int promoteAfterMeasuredCount,
        CancellationToken cancellationToken = default)
    {
        var grouped = await db.ShadowPicks
            .Where(x => x.OutcomeMeasured)
            .GroupBy(x => x.VariantMinPassScore)
            .Select(g => new
            {
                Variant = g.Key,
                Measured = g.Count(),
                Good = g.Count(x => x.OutcomeBucket == OutcomeBucketNames.Good),
                Flat = g.Count(x => x.OutcomeBucket == OutcomeBucketNames.Flat),
                Failed = g.Count(x => x.OutcomeBucket == OutcomeBucketNames.Failed),
            })
            .ToListAsync(cancellationToken);

        var existing = await db.ShadowVariantSummaries.ToListAsync(cancellationToken);
        db.ShadowVariantSummaries.RemoveRange(existing);

        var now = DateTime.UtcNow;
        var summaries = grouped
            .Select(g =>
            {
                var rate = g.Measured > 0
                    ? Math.Round(100m * g.Good / g.Measured, 1)
                    : 0m;
                return new ShadowVariantSummaryEntity
                {
                    VariantMinPassScore = g.Variant,
                    MeasuredCount = g.Measured,
                    GoodCount = g.Good,
                    FlatCount = g.Flat,
                    FailedCount = g.Failed,
                    SuccessRatePercent = rate,
                    IsProduction = g.Variant == productionMinPassScore,
                    IsLeader = false,
                    UpdatedAt = now,
                };
            })
            .ToList();

        var eligible = summaries
            .Where(s => s.MeasuredCount >= promoteAfterMeasuredCount)
            .OrderByDescending(s => s.SuccessRatePercent)
            .ThenByDescending(s => s.MeasuredCount)
            .ThenBy(s => Math.Abs(s.VariantMinPassScore - productionMinPassScore))
            .FirstOrDefault();

        var leader = eligible ?? summaries
            .Where(s => s.MeasuredCount > 0)
            .OrderByDescending(s => s.SuccessRatePercent)
            .ThenByDescending(s => s.MeasuredCount)
            .FirstOrDefault();

        if (leader is not null)
            leader.IsLeader = true;

        db.ShadowVariantSummaries.AddRange(summaries);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static ShadowPickRecord ToRecord(ShadowPickEntity e) => new(
        e.Id,
        e.ForTradingDate,
        e.VariantMinPassScore,
        e.Symbol,
        e.Rank,
        e.Score,
        e.EntryPrice,
        e.PredictedHitPercent,
        e.OutcomeMeasured,
        e.ForwardReturnPercent,
        e.OutcomeBucket,
        e.MeasuredAt);

    private static ShadowVariantSummaryRecord ToRecord(ShadowVariantSummaryEntity e) => new(
        e.VariantMinPassScore,
        e.MeasuredCount,
        e.GoodCount,
        e.FlatCount,
        e.FailedCount,
        e.SuccessRatePercent,
        e.IsProduction,
        e.IsLeader,
        e.UpdatedAt);

    public async Task ReplaceWeightPicksAsync(
        DateOnly forTradingDate,
        decimal weightMultiplier,
        IReadOnlyList<ShadowPickSeed> picks,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.ShadowWeightPicks
            .Where(x => x.ForTradingDate == forTradingDate && x.WeightMultiplier == weightMultiplier)
            .ToListAsync(cancellationToken);
        db.ShadowWeightPicks.RemoveRange(existing);

        foreach (var pick in picks)
        {
            db.ShadowWeightPicks.Add(new ShadowWeightPickEntity
            {
                Id = Guid.NewGuid(),
                ForTradingDate = forTradingDate,
                WeightMultiplier = weightMultiplier,
                Symbol = pick.Symbol,
                Rank = pick.Rank,
                Score = pick.Score,
                EntryPrice = pick.Price,
                PredictedHitPercent = pick.PredictedHitPercent,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShadowWeightPickRecord>> GetPendingWeightOutcomesAsync(
        DateOnly measureThroughDate,
        CancellationToken cancellationToken = default) =>
        await db.ShadowWeightPicks
            .Where(x => !x.OutcomeMeasured && x.ForTradingDate <= measureThroughDate)
            .OrderBy(x => x.ForTradingDate)
            .Select(x => ToWeightRecord(x))
            .ToListAsync(cancellationToken);

    public async Task UpdateWeightOutcomeAsync(
        Guid id,
        decimal forwardReturnPercent,
        string outcomeBucket,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.ShadowWeightPicks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
            return;

        entity.OutcomeMeasured = true;
        entity.ForwardReturnPercent = forwardReturnPercent;
        entity.OutcomeBucket = outcomeBucket;
        entity.MeasuredAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShadowWeightSummaryRecord>> GetWeightSummariesAsync(
        CancellationToken cancellationToken = default) =>
        await db.ShadowWeightSummaries
            .OrderBy(x => x.WeightMultiplier)
            .Select(x => ToWeightSummaryRecord(x))
            .ToListAsync(cancellationToken);

    public async Task RebuildWeightSummariesAsync(
        decimal productionMultiplier,
        int promoteAfterMeasuredCount,
        CancellationToken cancellationToken = default)
    {
        var grouped = await db.ShadowWeightPicks
            .Where(x => x.OutcomeMeasured)
            .GroupBy(x => x.WeightMultiplier)
            .Select(g => new
            {
                Multiplier = g.Key,
                Measured = g.Count(),
                Good = g.Count(x => x.OutcomeBucket == OutcomeBucketNames.Good),
                Flat = g.Count(x => x.OutcomeBucket == OutcomeBucketNames.Flat),
                Failed = g.Count(x => x.OutcomeBucket == OutcomeBucketNames.Failed),
            })
            .ToListAsync(cancellationToken);

        var existing = await db.ShadowWeightSummaries.ToListAsync(cancellationToken);
        db.ShadowWeightSummaries.RemoveRange(existing);

        var now = DateTime.UtcNow;
        var summaries = grouped
            .Select(g =>
            {
                var rate = g.Measured > 0
                    ? Math.Round(100m * g.Good / g.Measured, 1)
                    : 0m;
                return new ShadowWeightSummaryEntity
                {
                    WeightMultiplier = g.Multiplier,
                    MeasuredCount = g.Measured,
                    GoodCount = g.Good,
                    FlatCount = g.Flat,
                    FailedCount = g.Failed,
                    SuccessRatePercent = rate,
                    IsProduction = g.Multiplier == productionMultiplier,
                    IsLeader = false,
                    UpdatedAt = now,
                };
            })
            .ToList();

        var leader = summaries
            .Where(s => s.MeasuredCount >= promoteAfterMeasuredCount || s.MeasuredCount > 0)
            .OrderByDescending(s => s.MeasuredCount >= promoteAfterMeasuredCount)
            .ThenByDescending(s => s.SuccessRatePercent)
            .ThenByDescending(s => s.MeasuredCount)
            .FirstOrDefault();

        if (leader is not null)
            leader.IsLeader = true;

        db.ShadowWeightSummaries.AddRange(summaries);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static ShadowWeightPickRecord ToWeightRecord(ShadowWeightPickEntity e) => new(
        e.Id,
        e.ForTradingDate,
        e.WeightMultiplier,
        e.Symbol,
        e.Rank,
        e.Score,
        e.EntryPrice,
        e.PredictedHitPercent,
        e.OutcomeMeasured,
        e.ForwardReturnPercent,
        e.OutcomeBucket,
        e.MeasuredAt);

    private static ShadowWeightSummaryRecord ToWeightSummaryRecord(ShadowWeightSummaryEntity e) => new(
        e.WeightMultiplier,
        e.MeasuredCount,
        e.GoodCount,
        e.FlatCount,
        e.FailedCount,
        e.SuccessRatePercent,
        e.IsProduction,
        e.IsLeader,
        e.UpdatedAt);
}

internal sealed class EfEntryTimingRepository(ApplicationDbContext db) : IEntryTimingRepository
{
    public async Task<EntryTimingStateRecord?> GetAsync(CancellationToken cancellationToken = default)
    {
        var e = await db.EntryTimingStates.FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        return e is null
            ? null
            : new EntryTimingStateRecord(
                e.TopOnlyMeasured,
                e.TopOnlyGood,
                e.ConfirmMeasured,
                e.ConfirmGood,
                e.PreferMasterConfirm,
                e.UpdatedAt);
    }

    public async Task SaveAsync(EntryTimingStateRecord state, CancellationToken cancellationToken = default)
    {
        var e = await db.EntryTimingStates.FirstOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (e is null)
        {
            db.EntryTimingStates.Add(new EntryTimingStateEntity
            {
                Id = 1,
                TopOnlyMeasured = state.TopOnlyMeasured,
                TopOnlyGood = state.TopOnlyGood,
                ConfirmMeasured = state.ConfirmMeasured,
                ConfirmGood = state.ConfirmGood,
                PreferMasterConfirm = state.PreferMasterConfirm,
                UpdatedAt = state.UpdatedAt,
            });
        }
        else
        {
            e.TopOnlyMeasured = state.TopOnlyMeasured;
            e.TopOnlyGood = state.TopOnlyGood;
            e.ConfirmMeasured = state.ConfirmMeasured;
            e.ConfirmGood = state.ConfirmGood;
            e.PreferMasterConfirm = state.PreferMasterConfirm;
            e.UpdatedAt = state.UpdatedAt;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

internal sealed class EfTradeJournalRepository(ApplicationDbContext db) : ITradeJournalRepository
{
    public async Task AddAsync(TradeJournalRecord entry, CancellationToken cancellationToken = default)
    {
        db.TradeJournalEntries.Add(new TradeJournalEntryEntity
        {
            Id = entry.Id,
            UserId = entry.UserId,
            Symbol = entry.Symbol,
            TradeDate = entry.TradeDate,
            Action = entry.Action,
            SizePercent = entry.SizePercent,
            EngineVerdict = entry.EngineVerdict,
            Note = entry.Note,
            BuyScore = entry.BuyScore,
            PredictedHit = entry.PredictedHit,
            SetupDna = entry.SetupDna,
            CreatedAt = entry.CreatedAt,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TradeJournalRecord>> GetForUserAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default) =>
        await db.TradeJournalEntries
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .Select(x => new TradeJournalRecord(
                x.Id,
                x.UserId,
                x.Symbol,
                x.TradeDate,
                x.Action,
                x.SizePercent,
                x.EngineVerdict,
                x.Note,
                x.BuyScore,
                x.PredictedHit,
                x.SetupDna,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<PersonalCalibrationRecord?> GetCalibrationAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var e = await db.PersonalCalibrationStates
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        return e is null ? null : new PersonalCalibrationRecord(e.Factor, e.SampleCount, e.UpdatedAt);
    }

    public async Task SaveCalibrationAsync(
        Guid userId,
        PersonalCalibrationRecord calibration,
        CancellationToken cancellationToken = default)
    {
        var e = await db.PersonalCalibrationStates
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (e is null)
        {
            db.PersonalCalibrationStates.Add(new PersonalCalibrationStateEntity
            {
                UserId = userId,
                Factor = calibration.Factor,
                SampleCount = calibration.SampleCount,
                UpdatedAt = calibration.UpdatedAt,
            });
        }
        else
        {
            e.Factor = calibration.Factor;
            e.SampleCount = calibration.SampleCount;
            e.UpdatedAt = calibration.UpdatedAt;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
