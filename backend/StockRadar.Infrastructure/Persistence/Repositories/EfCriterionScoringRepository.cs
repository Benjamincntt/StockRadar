using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.Persistence.Entities;
using StockRadar.Infrastructure.Persistence.Mapping;

namespace StockRadar.Infrastructure.Persistence.Repositories;

internal sealed class EfCriterionScoringRepository(ApplicationDbContext db) : ICriterionScoringRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<IReadOnlyDictionary<CriterionType, decimal>> GetWeightsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await db.CriterionWeights.AsNoTracking().ToListAsync(cancellationToken);
        if (rows.Count == 0)
            return DefaultWeights();

        return rows.ToDictionary(
            r => Enum.Parse<CriterionType>(r.CriterionId),
            r => r.IsActive ? r.Weight : 0.25m);
    }

    public async Task<IReadOnlyList<CriterionWeight>> GetWeightDetailsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await db.CriterionWeights.AsNoTracking().ToListAsync(cancellationToken);
        return rows.Select(MapWeight).ToList();
    }

    public async Task UpsertWeightsAsync(
        IReadOnlyList<CriterionWeight> weights,
        CancellationToken cancellationToken = default)
    {
        foreach (var w in weights)
        {
            var id = w.Type.ToString();
            var entity = await db.CriterionWeights.FirstOrDefaultAsync(x => x.CriterionId == id, cancellationToken);
            if (entity is null)
            {
                db.CriterionWeights.Add(new CriterionWeightEntity
                {
                    CriterionId = id,
                    GroupId = CriterionLabels.GetGroup(w.Type),
                    Rank = CriterionLabels.GetRank(w.Type),
                    Weight = w.Weight,
                    Accuracy7d = w.Accuracy7d,
                    Accuracy30d = w.Accuracy30d,
                    SampleCount7d = w.SampleCount7d,
                    Reliability7d = w.Reliability7d,
                    Edge7d = w.Edge7d,
                    IsActive = w.IsActive,
                    RecommendedAction = w.RecommendedAction.ToString(),
                    UpdatedAt = DateTime.UtcNow,
                });
            }
            else
            {
                entity.GroupId = CriterionLabels.GetGroup(w.Type);
                entity.Rank = CriterionLabels.GetRank(w.Type);
                entity.Weight = w.Weight;
                entity.Accuracy7d = w.Accuracy7d;
                entity.Accuracy30d = w.Accuracy30d;
                entity.SampleCount7d = w.SampleCount7d;
                entity.Reliability7d = w.Reliability7d;
                entity.Edge7d = w.Edge7d;
                entity.IsActive = w.IsActive;
                entity.RecommendedAction = w.RecommendedAction.ToString();
                entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceDailyAccuracyAsync(
        DateOnly asOfDate,
        int horizon,
        IReadOnlyList<CriterionAccuracySnapshot> snapshots,
        DateTime generatedAt,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.DailyCriterionAccuracies
            .Where(x => x.AsOfDate == asOfDate && x.Horizon == horizon)
            .ToListAsync(cancellationToken);
        db.DailyCriterionAccuracies.RemoveRange(existing);

        foreach (var s in snapshots)
        {
            db.DailyCriterionAccuracies.Add(new DailyCriterionAccuracyEntity
            {
                AsOfDate = asOfDate,
                Horizon = horizon,
                CriterionId = s.Type.ToString(),
                GroupId = CriterionLabels.GetGroup(s.Type),
                Rank = CriterionLabels.GetRank(s.Type),
                HitCount = s.HitCount,
                TotalCount = s.TotalCount,
                AccuracyPercent = s.AccuracyPercent,
                AvgScore = s.AvgScore,
                AvgMfePercent = s.AvgMfePercent,
                AvgMaePercent = s.AvgMaePercent,
                InvalidationRatePercent = s.InvalidationRatePercent,
                BaselinePercent = s.BaselinePercent,
                EdgePercent = s.EdgePercent,
                ReliabilityScore = s.ReliabilityScore,
                BreakdownJson = CriterionBreakdownMapper.ToJson(s),
                GeneratedAt = generatedAt,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceGroupDailyAccuracyAsync(
        DateOnly asOfDate,
        int horizon,
        IReadOnlyList<CriterionGroupAccuracySnapshot> snapshots,
        DateTime generatedAt,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.CriterionGroupDailyAccuracies
            .Where(x => x.AsOfDate == asOfDate && x.Horizon == horizon)
            .ToListAsync(cancellationToken);
        db.CriterionGroupDailyAccuracies.RemoveRange(existing);

        foreach (var g in snapshots)
        {
            db.CriterionGroupDailyAccuracies.Add(new CriterionGroupDailyAccuracyEntity
            {
                AsOfDate = asOfDate,
                Horizon = horizon,
                GroupId = g.GroupId,
                HitCount = g.HitCount,
                TotalCount = g.TotalCount,
                AccuracyPercent = g.AccuracyPercent,
                AvgScore = g.AvgScore,
                CriterionCount = g.CriterionCount,
                ReliabilityScore = g.ReliabilityScore,
                EdgePercent = g.EdgePercent,
                GeneratedAt = generatedAt,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CriterionAccuracySnapshot>> GetDailyAccuracyAsync(
        DateOnly asOfDate,
        int horizon = 5,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.DailyCriterionAccuracies.AsNoTracking()
            .Where(x => x.AsOfDate == asOfDate && x.Horizon == horizon)
            .ToListAsync(cancellationToken);

        return rows.Select(MapDailySnapshot).ToList();
    }

    public async Task<IReadOnlyList<CriterionGroupAccuracySnapshot>> GetGroupDailyAccuracyAsync(
        DateOnly asOfDate,
        int horizon = 5,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.CriterionGroupDailyAccuracies.AsNoTracking()
            .Where(x => x.AsOfDate == asOfDate && x.Horizon == horizon)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new CriterionGroupAccuracySnapshot(
            r.GroupId,
            r.HitCount,
            r.TotalCount,
            r.AccuracyPercent,
            r.AvgScore,
            r.CriterionCount,
            r.ReliabilityScore,
            r.EdgePercent)).ToList();
    }

    public async Task<DateOnly?> GetLatestAccuracyDateAsync(
        int horizon = 5,
        CancellationToken cancellationToken = default)
    {
        return await db.DailyCriterionAccuracies.AsNoTracking()
            .Where(x => x.Horizon == horizon)
            .OrderByDescending(x => x.AsOfDate)
            .Select(x => (DateOnly?)x.AsOfDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> CountAccuracyDatesAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int horizon = 5,
        CancellationToken cancellationToken = default)
    {
        return await db.DailyCriterionAccuracies.AsNoTracking()
            .Where(x => x.AsOfDate >= fromDate && x.AsOfDate <= toDate && x.Horizon == horizon)
            .Select(x => x.AsOfDate)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CriterionAccuracySnapshot>> GetAccuracyRollingAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int horizon = 5,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.DailyCriterionAccuracies.AsNoTracking()
            .Where(x => x.AsOfDate >= fromDate && x.AsOfDate <= toDate && x.Horizon == horizon)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.CriterionId)
            .Select(g => MapRollingSnapshot(g.ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<CriterionAccuracyDailyPoint>> GetDailyAccuracySeriesAsync(
        DateOnly fromDate,
        DateOnly toDate,
        int horizon = 5,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.DailyCriterionAccuracies.AsNoTracking()
            .Where(x => x.AsOfDate >= fromDate && x.AsOfDate <= toDate && x.Horizon == horizon)
            .OrderBy(x => x.AsOfDate)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new CriterionAccuracyDailyPoint(r.AsOfDate, MapDailySnapshot(r)))
            .ToList();
    }

    public async Task ReplaceStockScoresAsync(
        DateOnly asOfDate,
        IReadOnlyList<StockCriterionScoreRecord> scores,
        DateTime generatedAt,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.StockCriterionScores
            .Where(x => x.AsOfDate == asOfDate)
            .ToListAsync(cancellationToken);
        db.StockCriterionScores.RemoveRange(existing);

        foreach (var s in scores)
        {
            db.StockCriterionScores.Add(new StockCriterionScoreEntity
            {
                AsOfDate = asOfDate,
                Symbol = s.Symbol,
                CompositeScore = s.CompositeScore,
                NextDayChangePercent = s.ForwardChangePercent,
                ScoresJson = JsonSerializer.Serialize(s.Scores, JsonOptions),
                GeneratedAt = generatedAt,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceStockDetailsAsync(
        DateOnly asOfDate,
        int horizon,
        IReadOnlyList<StockCriterionDetailRecord> details,
        DateTime generatedAt,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.StockCriterionDetails
            .Where(x => x.AsOfDate == asOfDate && x.Horizon == horizon)
            .ToListAsync(cancellationToken);
        db.StockCriterionDetails.RemoveRange(existing);

        const int batchSize = 500;
        for (var i = 0; i < details.Count; i += batchSize)
        {
            foreach (var d in details.Skip(i).Take(batchSize))
            {
                db.StockCriterionDetails.Add(new StockCriterionDetailEntity
                {
                    AsOfDate = asOfDate,
                    Horizon = horizon,
                    Symbol = d.Symbol,
                    CriterionId = d.Type.ToString(),
                    GroupId = d.GroupId,
                    Rank = d.Rank,
                    Score = d.Score,
                    Bias = d.Bias.ToString(),
                    Summary = d.Summary.Length > 256 ? d.Summary[..256] : d.Summary,
                    NextDayChangePercent = d.ForwardChangePercent,
                    MatchedOutcome = d.MatchedOutcome,
                    MaxFavorablePercent = d.MaxFavorablePercent,
                    MaxAdversePercent = d.MaxAdversePercent,
                    InvalidatedBase = d.InvalidatedBase,
                    RelativeStrengthForward = d.RelativeStrengthForward,
                    ScoreBucket = d.ScoreBucket,
                    MarketPhase = d.MarketPhase.ToString(),
                    GeneratedAt = generatedAt,
                });
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpsertWeeklyReviewsAsync(
        DateOnly weekStart,
        IReadOnlyList<WeeklyCriterionReviewSnapshot> criteria,
        IReadOnlyList<CriterionGroupWeeklySnapshot> groups,
        DateTime generatedAt,
        CancellationToken cancellationToken = default)
    {
        var existingCriteria = await db.WeeklyCriterionReviews
            .Where(x => x.WeekStartDate == weekStart)
            .ToListAsync(cancellationToken);
        db.WeeklyCriterionReviews.RemoveRange(existingCriteria);

        foreach (var c in criteria)
        {
            db.WeeklyCriterionReviews.Add(new WeeklyCriterionReviewEntity
            {
                WeekStartDate = weekStart,
                CriterionId = c.Type.ToString(),
                GroupId = c.GroupId,
                Label = c.Label,
                Rank = c.Rank,
                HitCount7d = c.HitCount7d,
                TotalCount7d = c.TotalCount7d,
                Accuracy7d = c.Accuracy7d,
                AvgScore7d = c.AvgScore7d,
                Weight = c.Weight,
                Edge7d = c.Edge7d,
                Reliability7d = c.Reliability7d,
                AvgMfe7d = c.AvgMfe7d,
                InvalidationRate7d = c.InvalidationRate7d,
                BreakdownJson = CriterionBreakdownMapper.ToJson(new CriterionAccuracySnapshot(
                    c.Type,
                    c.HitCount7d,
                    c.TotalCount7d,
                    c.Accuracy7d,
                    c.AvgScore7d,
                    c.AvgMfe7d,
                    0,
                    c.InvalidationRate7d,
                    0,
                    c.Edge7d,
                    c.Reliability7d,
                    c.Buckets,
                    c.Phases)),
                RecommendedAction = c.RecommendedAction.ToString(),
                IsActive = c.IsActive,
                GeneratedAt = generatedAt,
            });
        }

        var existingGroups = await db.CriterionGroupWeeklyReviews
            .Where(x => x.WeekStartDate == weekStart)
            .ToListAsync(cancellationToken);
        db.CriterionGroupWeeklyReviews.RemoveRange(existingGroups);

        foreach (var g in groups)
        {
            db.CriterionGroupWeeklyReviews.Add(new CriterionGroupWeeklyReviewEntity
            {
                WeekStartDate = weekStart,
                GroupId = g.GroupId,
                HitCount = g.HitCount,
                TotalCount = g.TotalCount,
                AccuracyPercent = g.AccuracyPercent,
                AvgScore = g.AvgScore,
                KeepCount = g.KeepCount,
                WatchCount = g.WatchCount,
                RemoveCount = g.RemoveCount,
                RecommendedAction = g.RecommendedAction.ToString(),
                GeneratedAt = generatedAt,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WeeklyCriterionReviewSnapshot>> GetWeeklyReviewsAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.WeeklyCriterionReviews.AsNoTracking()
            .Where(x => x.WeekStartDate == weekStart)
            .ToListAsync(cancellationToken);

        return rows.Select(r =>
        {
            var (buckets, phases) = CriterionBreakdownMapper.FromJson(r.BreakdownJson);
            return new WeeklyCriterionReviewSnapshot(
                Enum.Parse<CriterionType>(r.CriterionId),
                r.GroupId,
                r.Label,
                r.Rank,
                r.HitCount7d,
                r.TotalCount7d,
                r.Accuracy7d,
                r.AvgScore7d,
                r.Weight,
                Enum.Parse<CriterionReviewAction>(r.RecommendedAction),
                r.IsActive,
                r.Edge7d,
                r.Reliability7d,
                r.AvgMfe7d,
                r.InvalidationRate7d,
                buckets,
                phases);
        }).ToList();
    }

    public async Task<IReadOnlyList<CriterionGroupWeeklySnapshot>> GetGroupWeeklyReviewsAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.CriterionGroupWeeklyReviews.AsNoTracking()
            .Where(x => x.WeekStartDate == weekStart)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new CriterionGroupWeeklySnapshot(
            r.GroupId,
            r.HitCount,
            r.TotalCount,
            r.AccuracyPercent,
            r.AvgScore,
            r.KeepCount,
            r.WatchCount,
            r.RemoveCount,
            Enum.Parse<CriterionReviewAction>(r.RecommendedAction))).ToList();
    }

    public async Task<StockCriterionScoreRecord?> GetStockScoreAsync(
        DateOnly asOfDate,
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var row = await db.StockCriterionScores.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.AsOfDate == asOfDate && x.Symbol == symbol,
                cancellationToken);

        if (row is null)
            return null;

        var scores = JsonSerializer.Deserialize<List<CriterionScore>>(row.ScoresJson, JsonOptions) ?? [];
        return new StockCriterionScoreRecord(
            row.AsOfDate,
            row.Symbol,
            row.CompositeScore,
            row.NextDayChangePercent,
            scores);
    }

    public async Task<IReadOnlyList<StockCriterionScoreRecord>> GetTopStockScoresAsync(
        DateOnly asOfDate,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.StockCriterionScores.AsNoTracking()
            .Where(x => x.AsOfDate == asOfDate)
            .OrderByDescending(x => x.CompositeScore)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows.Select(row =>
        {
            var scores = JsonSerializer.Deserialize<List<CriterionScore>>(row.ScoresJson, JsonOptions) ?? [];
            return new StockCriterionScoreRecord(
                row.AsOfDate,
                row.Symbol,
                row.CompositeScore,
                row.NextDayChangePercent,
                scores);
        }).ToList();
    }

    public async Task<IReadOnlyDictionary<string, int>> GetCompositeScoresBySymbolsAsync(
        DateOnly asOfDate,
        IReadOnlyList<string> symbols,
        CancellationToken cancellationToken = default)
    {
        if (symbols.Count == 0)
            return new Dictionary<string, int>();

        var normalized = symbols
            .Select(s => s.Trim().ToUpperInvariant())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = await db.StockCriterionScores.AsNoTracking()
            .Where(x => x.AsOfDate == asOfDate && normalized.Contains(x.Symbol))
            .Select(x => new { x.Symbol, x.CompositeScore })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Symbol, r => r.CompositeScore, StringComparer.OrdinalIgnoreCase);
    }

    private static CriterionWeight MapWeight(CriterionWeightEntity e) => new(
        Enum.Parse<CriterionType>(e.CriterionId),
        e.Weight,
        e.Accuracy7d,
        e.SampleCount7d,
        e.Accuracy30d,
        e.IsActive,
        Enum.Parse<CriterionReviewAction>(e.RecommendedAction),
        e.Reliability7d,
        e.Edge7d);

    private static CriterionAccuracySnapshot MapDailySnapshot(DailyCriterionAccuracyEntity r)
    {
        var (buckets, phases) = CriterionBreakdownMapper.FromJson(r.BreakdownJson);
        return new(
            Enum.Parse<CriterionType>(r.CriterionId),
            r.HitCount,
            r.TotalCount,
            r.AccuracyPercent,
            r.AvgScore,
            r.AvgMfePercent,
            r.AvgMaePercent,
            r.InvalidationRatePercent,
            r.BaselinePercent,
            r.EdgePercent,
            r.ReliabilityScore,
            buckets,
            phases);
    }

    private static CriterionAccuracySnapshot MapRollingSnapshot(
        IReadOnlyList<DailyCriterionAccuracyEntity> rows)
    {
        var hits = rows.Sum(x => x.HitCount);
        var total = rows.Sum(x => x.TotalCount);
        var hitRate = total > 0 ? Math.Round((decimal)hits / total * 100m, 1) : 0m;
        var avgScore = WeightedAverage(rows, x => x.AvgScore);
        var avgMfe = WeightedAverage(rows, x => x.AvgMfePercent);
        var avgMae = WeightedAverage(rows, x => x.AvgMaePercent);
        var invalidation = WeightedAverage(rows, x => x.InvalidationRatePercent);
        var baseline = WeightedAverage(rows, x => x.BaselinePercent);
        var edge = Math.Round(hitRate - baseline, 1);
        var reliability = WeightedAverage(rows, x => x.ReliabilityScore);
        var bucketSources = rows.Select(r => CriterionBreakdownMapper.FromJson(r.BreakdownJson).Buckets);
        var phaseSources = rows.Select(r => CriterionBreakdownMapper.FromJson(r.BreakdownJson).Phases);

        return new(
            Enum.Parse<CriterionType>(rows[0].CriterionId),
            hits,
            total,
            hitRate,
            avgScore,
            avgMfe,
            avgMae,
            invalidation,
            baseline,
            edge,
            reliability,
            CriterionBreakdownMapper.MergeBuckets(bucketSources),
            CriterionBreakdownMapper.MergePhases(phaseSources));
    }

    private static decimal WeightedAverage(
        IReadOnlyList<DailyCriterionAccuracyEntity> rows,
        Func<DailyCriterionAccuracyEntity, decimal> selector)
    {
        var total = rows.Sum(x => x.TotalCount);
        if (total <= 0)
            return 0;
        return Math.Round(rows.Sum(x => selector(x) * x.TotalCount) / total, 2);
    }

    private static Dictionary<CriterionType, decimal> DefaultWeights()
    {
        var dict = new Dictionary<CriterionType, decimal>();
        foreach (CriterionType t in Enum.GetValues<CriterionType>())
            dict[t] = 1m;
        return dict;
    }
}
