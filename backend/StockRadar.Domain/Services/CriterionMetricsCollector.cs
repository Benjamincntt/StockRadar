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

    // Keyed by (type, playbookId)
    private readonly Dictionary<(CriterionType, string), MetricState> _criteria = new();
    // Keyed by (groupId, playbookId)
    private readonly Dictionary<(string, string), MetricState> _groups = new();
    private readonly Dictionary<(string, string), HashSet<CriterionType>> _groupCriterionTypes = new();
    // Baseline per playbookId
    private readonly Dictionary<string, (int Hits, int Total)> _baseline = new(StringComparer.Ordinal);

    public void RecordBaseline(bool hit, string playbookId = "unclassified")
    {
        _baseline.TryGetValue(playbookId, out var cur);
        _baseline[playbookId] = (cur.Hits + (hit ? 1 : 0), cur.Total + 1);
    }

    public void Record(
        CriterionType type,
        string groupId,
        int score,
        string bucket,
        MarketWyckoffPhase phase,
        bool hit,
        CriterionForwardOutcome outcome,
        string playbookId = "unclassified")
    {
        RecordState(_criteria, (type, playbookId), score, bucket, phase, hit, outcome);
        RecordState(_groups, (groupId, playbookId), score, bucket, phase, hit, outcome);

        var groupKey = (groupId, playbookId);
        if (!_groupCriterionTypes.TryGetValue(groupKey, out var types))
        {
            types = [];
            _groupCriterionTypes[groupKey] = types;
        }
        types.Add(type);
    }

    public decimal BaselinePercent => GetBaselinePercent("unclassified");

    public decimal GetBaselinePercent(string playbookId)
    {
        if (!_baseline.TryGetValue(playbookId, out var b) || b.Total == 0) return 0m;
        return Math.Round((decimal)b.Hits / b.Total * 100m, 1);
    }

    /// <summary>Tất cả playbook đã có ít nhất 1 record.</summary>
    public IReadOnlySet<string> PlaybookIds =>
        _criteria.Keys.Select(k => k.Item2).ToHashSet(StringComparer.Ordinal);

    public IReadOnlyList<CriterionAccuracySnapshot> BuildCriterionSnapshots(
        ICriterionAccuracyEvaluator evaluator,
        string? playbookId = null)
    {
        return _criteria
            .Where(kv => playbookId is null || kv.Key.Item2 == playbookId)
            .Select(kv =>
            {
                var baseline = GetBaselinePercent(kv.Key.Item2);
                return BuildSnapshot(kv.Key.Item1, kv.Key.Item2, kv.Value, baseline, evaluator);
            })
            .ToList();
    }

    public IReadOnlyList<CriterionGroupAccuracySnapshot> BuildGroupSnapshots(
        ICriterionAccuracyEvaluator evaluator,
        string? playbookId = null)
    {
        return _groups
            .Where(kv => playbookId is null || kv.Key.Item2 == playbookId)
            .Select(kv =>
            {
                var baseline = GetBaselinePercent(kv.Key.Item2);
                var snap = BuildSnapshotMetrics(kv.Value, baseline, evaluator);
                var criterionCount = _groupCriterionTypes.GetValueOrDefault(kv.Key)?.Count ?? 0;
                return new CriterionGroupAccuracySnapshot(
                    kv.Key.Item1,
                    snap.Hits,
                    snap.Total,
                    snap.HitRate,
                    snap.AvgScore,
                    criterionCount,
                    snap.Reliability,
                    snap.Edge,
                    kv.Key.Item2);
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
        string playbookId,
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
            phases,
            playbookId);
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
