using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using StockRadar.Application.Abstractions;
using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;
using StockRadar.Infrastructure.Persistence.Entities;

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
                entity.IsActive = w.IsActive;
                entity.RecommendedAction = w.RecommendedAction.ToString();
                entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceDailyAccuracyAsync(
        DateOnly asOfDate,
        IReadOnlyList<CriterionAccuracySnapshot> snapshots,
        DateTime generatedAt,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.DailyCriterionAccuracies
            .Where(x => x.AsOfDate == asOfDate)
            .ToListAsync(cancellationToken);
        db.DailyCriterionAccuracies.RemoveRange(existing);

        foreach (var s in snapshots)
        {
            db.DailyCriterionAccuracies.Add(new DailyCriterionAccuracyEntity
            {
                AsOfDate = asOfDate,
                CriterionId = s.Type.ToString(),
                GroupId = CriterionLabels.GetGroup(s.Type),
                Rank = CriterionLabels.GetRank(s.Type),
                HitCount = s.HitCount,
                TotalCount = s.TotalCount,
                AccuracyPercent = s.AccuracyPercent,
                AvgScore = s.AvgScore,
                GeneratedAt = generatedAt,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceGroupDailyAccuracyAsync(
        DateOnly asOfDate,
        IReadOnlyList<CriterionGroupAccuracySnapshot> snapshots,
        DateTime generatedAt,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.CriterionGroupDailyAccuracies
            .Where(x => x.AsOfDate == asOfDate)
            .ToListAsync(cancellationToken);
        db.CriterionGroupDailyAccuracies.RemoveRange(existing);

        foreach (var g in snapshots)
        {
            db.CriterionGroupDailyAccuracies.Add(new CriterionGroupDailyAccuracyEntity
            {
                AsOfDate = asOfDate,
                GroupId = g.GroupId,
                HitCount = g.HitCount,
                TotalCount = g.TotalCount,
                AccuracyPercent = g.AccuracyPercent,
                AvgScore = g.AvgScore,
                CriterionCount = g.CriterionCount,
                GeneratedAt = generatedAt,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CriterionAccuracySnapshot>> GetDailyAccuracyAsync(
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.DailyCriterionAccuracies.AsNoTracking()
            .Where(x => x.AsOfDate == asOfDate)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new CriterionAccuracySnapshot(
            Enum.Parse<CriterionType>(r.CriterionId),
            r.HitCount,
            r.TotalCount,
            r.AccuracyPercent,
            r.AvgScore)).ToList();
    }

    public async Task<IReadOnlyList<CriterionGroupAccuracySnapshot>> GetGroupDailyAccuracyAsync(
        DateOnly asOfDate,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.CriterionGroupDailyAccuracies.AsNoTracking()
            .Where(x => x.AsOfDate == asOfDate)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new CriterionGroupAccuracySnapshot(
            r.GroupId,
            r.HitCount,
            r.TotalCount,
            r.AccuracyPercent,
            r.AvgScore,
            r.CriterionCount)).ToList();
    }

    public async Task<DateOnly?> GetLatestAccuracyDateAsync(CancellationToken cancellationToken = default)
    {
        return await db.DailyCriterionAccuracies.AsNoTracking()
            .OrderByDescending(x => x.AsOfDate)
            .Select(x => (DateOnly?)x.AsOfDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CriterionAccuracySnapshot>> GetAccuracyRollingAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.DailyCriterionAccuracies.AsNoTracking()
            .Where(x => x.AsOfDate >= fromDate && x.AsOfDate <= toDate)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.CriterionId)
            .Select(g =>
            {
                var hits = g.Sum(x => x.HitCount);
                var total = g.Sum(x => x.TotalCount);
                var pct = total > 0 ? Math.Round((decimal)hits / total * 100m, 1) : 0m;
                var avgScore = g.Any() ? Math.Round(g.Average(x => x.AvgScore), 1) : 0m;
                return new CriterionAccuracySnapshot(
                    Enum.Parse<CriterionType>(g.Key),
                    hits,
                    total,
                    pct,
                    avgScore);
            })
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
                NextDayChangePercent = s.NextDayChangePercent,
                ScoresJson = JsonSerializer.Serialize(s.Scores, JsonOptions),
                GeneratedAt = generatedAt,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceStockDetailsAsync(
        DateOnly asOfDate,
        IReadOnlyList<StockCriterionDetailRecord> details,
        DateTime generatedAt,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.StockCriterionDetails
            .Where(x => x.AsOfDate == asOfDate)
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
                    Symbol = d.Symbol,
                    CriterionId = d.Type.ToString(),
                    GroupId = d.GroupId,
                    Rank = d.Rank,
                    Score = d.Score,
                    Bias = d.Bias.ToString(),
                    Summary = d.Summary.Length > 256 ? d.Summary[..256] : d.Summary,
                    NextDayChangePercent = d.NextDayChangePercent,
                    MatchedOutcome = d.MatchedOutcome,
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

        return rows.Select(r => new WeeklyCriterionReviewSnapshot(
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
            r.IsActive)).ToList();
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

    private static CriterionWeight MapWeight(CriterionWeightEntity e) => new(
        Enum.Parse<CriterionType>(e.CriterionId),
        e.Weight,
        e.Accuracy7d,
        e.SampleCount7d,
        e.Accuracy30d,
        e.IsActive,
        Enum.Parse<CriterionReviewAction>(e.RecommendedAction));

    private static Dictionary<CriterionType, decimal> DefaultWeights()
    {
        var dict = new Dictionary<CriterionType, decimal>();
        foreach (CriterionType t in Enum.GetValues<CriterionType>())
            dict[t] = 1m;
        return dict;
    }
}
