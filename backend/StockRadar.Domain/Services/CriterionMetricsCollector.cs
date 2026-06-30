using StockRadar.Domain.Enums;
using StockRadar.Domain.ValueObjects;

namespace StockRadar.Domain.Services;

public sealed class CriterionMetricsCollector
{
    private sealed class MetricState
    {
        public int Hits;
        public int Total;
        public decimal ScoreSum;
        public decimal MfeSum;
        public decimal MaeSum;
        public int Invalidations;
        public readonly Dictionary<string, (int Hits, int Total)> Buckets = new(StringComparer.Ordinal);
        public readonly Dictionary<MarketWyckoffPhase, (int Hits, int Total)> Phases = new();
    }

    private readonly Dictionary<CriterionType, MetricState> _criteria = new();
    private readonly Dictionary<string, MetricState> _groups = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<CriterionType>> _groupCriterionTypes = new(StringComparer.OrdinalIgnoreCase);
    private int _baselineHits;
    private int _baselineTotal;

    public void RecordBaseline(bool hit)
    {
        _baselineTotal++;
        if (hit)
            _baselineHits++;
    }

    public void Record(
        CriterionType type,
        string groupId,
        int score,
        string bucket,
        MarketWyckoffPhase phase,
        bool hit,
        CriterionForwardOutcome outcome)
    {
        RecordState(_criteria, type, score, bucket, phase, hit, outcome);
        RecordState(_groups, groupId, score, bucket, phase, hit, outcome);

        if (!_groupCriterionTypes.TryGetValue(groupId, out var types))
        {
            types = [];
            _groupCriterionTypes[groupId] = types;
        }

        types.Add(type);
    }

    public decimal BaselinePercent =>
        _baselineTotal > 0 ? Math.Round((decimal)_baselineHits / _baselineTotal * 100m, 1) : 0m;

    public IReadOnlyList<CriterionAccuracySnapshot> BuildCriterionSnapshots(
        ICriterionAccuracyEvaluator evaluator)
    {
        var baseline = BaselinePercent;
        return _criteria
            .Select(kv => BuildSnapshot(kv.Key, kv.Value, baseline, evaluator))
            .ToList();
    }

    public IReadOnlyList<CriterionGroupAccuracySnapshot> BuildGroupSnapshots(
        ICriterionAccuracyEvaluator evaluator)
    {
        var baseline = BaselinePercent;
        return _groups
            .Select(kv =>
            {
                var snap = BuildSnapshotMetrics(kv.Value, baseline, evaluator);
                return new CriterionGroupAccuracySnapshot(
                    kv.Key,
                    snap.Hits,
                    snap.Total,
                    snap.HitRate,
                    snap.AvgScore,
                    _groupCriterionTypes.GetValueOrDefault(kv.Key)?.Count ?? 0,
                    snap.Reliability,
                    snap.Edge);
            })
            .ToList();
    }

    private static void RecordState<TKey>(
        Dictionary<TKey, MetricState> map,
        TKey key,
        int score,
        string bucket,
        MarketWyckoffPhase phase,
        bool hit,
        CriterionForwardOutcome outcome)
        where TKey : notnull
    {
        if (!map.TryGetValue(key, out var state))
        {
            state = new MetricState();
            map[key] = state;
        }

        state.Total++;
        state.ScoreSum += score;
        state.MfeSum += outcome.MaxFavorablePercent;
        state.MaeSum += outcome.MaxAdversePercent;
        if (outcome.InvalidatedBase)
            state.Invalidations++;
        if (hit)
            state.Hits++;

        if (!state.Buckets.TryGetValue(bucket, out var bucketStats))
            bucketStats = (0, 0);
        bucketStats.Total++;
        if (hit)
            bucketStats.Hits++;
        state.Buckets[bucket] = bucketStats;

        if (!state.Phases.TryGetValue(phase, out var phaseStats))
            phaseStats = (0, 0);
        phaseStats.Total++;
        if (hit)
            phaseStats.Hits++;
        state.Phases[phase] = phaseStats;
    }

    private static CriterionAccuracySnapshot BuildSnapshot(
        CriterionType type,
        MetricState state,
        decimal baselinePercent,
        ICriterionAccuracyEvaluator evaluator)
    {
        var metrics = BuildSnapshotMetrics(state, baselinePercent, evaluator);
        var buckets = state.Buckets
            .OrderBy(b => b.Key)
            .Select(b => new CriterionScoreBucketStats(
                b.Key,
                b.Value.Hits,
                b.Value.Total,
                b.Value.Total > 0 ? Math.Round((decimal)b.Value.Hits / b.Value.Total * 100m, 1) : 0m))
            .ToList();

        var phases = state.Phases
            .OrderBy(p => p.Key)
            .Select(p => new CriterionPhaseStats(
                p.Key,
                p.Value.Hits,
                p.Value.Total,
                p.Value.Total > 0 ? Math.Round((decimal)p.Value.Hits / p.Value.Total * 100m, 1) : 0m))
            .ToList();

        return new CriterionAccuracySnapshot(
            type,
            metrics.Hits,
            metrics.Total,
            metrics.HitRate,
            metrics.AvgScore,
            metrics.AvgMfe,
            metrics.AvgMae,
            metrics.InvalidationRate,
            baselinePercent,
            metrics.Edge,
            metrics.Reliability,
            buckets,
            phases);
    }

    private static (
        int Hits,
        int Total,
        decimal HitRate,
        decimal AvgScore,
        decimal AvgMfe,
        decimal AvgMae,
        decimal InvalidationRate,
        decimal Edge,
        decimal Reliability) BuildSnapshotMetrics(
        MetricState state,
        decimal baselinePercent,
        ICriterionAccuracyEvaluator evaluator)
    {
        var hitRate = state.Total > 0 ? Math.Round((decimal)state.Hits / state.Total * 100m, 1) : 0m;
        var avgScore = state.Total > 0 ? Math.Round(state.ScoreSum / state.Total, 1) : 0m;
        var avgMfe = state.Total > 0 ? Math.Round(state.MfeSum / state.Total, 2) : 0m;
        var avgMae = state.Total > 0 ? Math.Round(state.MaeSum / state.Total, 2) : 0m;
        var invalidationRate = state.Total > 0
            ? Math.Round((decimal)state.Invalidations / state.Total * 100m, 1)
            : 0m;
        var edge = Math.Round(hitRate - baselinePercent, 1);
        var reliability = evaluator.ComputeReliabilityScore(hitRate, edge, avgMfe, invalidationRate);

        return (
            state.Hits,
            state.Total,
            hitRate,
            avgScore,
            avgMfe,
            avgMae,
            invalidationRate,
            edge,
            reliability);
    }
}
