using StockRadar.Domain.Enums;

namespace StockRadar.Domain.ValueObjects;

public sealed record AdaptiveCriterionState(
    decimal WeightMultiplier,
    decimal ReliabilityPercent,
    int SampleCount,
    bool IsActive,
    int BaseMaxPoints)
{
    public int EffectiveMaxPoints =>
        IsActive
            ? Math.Max(1, (int)Math.Round(BaseMaxPoints * Math.Clamp(WeightMultiplier, 0.25m, 2.5m)))
            : Math.Max(0, (int)Math.Round(BaseMaxPoints * 0.25m));
}

/// <summary>Trọng số động từ CriterionWeights — áp vào Buy Score SmartMoney.</summary>
public sealed class AdaptiveScoringProfile
{
    public static readonly IReadOnlyDictionary<string, int> BaseMaxPoints =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["market"] = 12,
            ["sector"] = 18,
            ["rs"] = 20,
            ["base"] = 18,
            ["breakout"] = 22,
            ["shakeout"] = 10,
            ["volume"] = 8,
            ["wyckoff"] = 5,
            ["trend"] = 5,
        };

    public static readonly IReadOnlyDictionary<CriterionType, string> CriterionToComponent =
        new Dictionary<CriterionType, string>
        {
            [CriterionType.MarketPhase] = "market",
            [CriterionType.SectorStrength] = "sector",
            [CriterionType.RelativeStrength5d] = "rs",
            [CriterionType.BaseSetup] = "base",
            [CriterionType.BreakoutVolume] = "breakout",
            [CriterionType.ShakeoutRecovery] = "shakeout",
            [CriterionType.VolumeSpike] = "volume",
            [CriterionType.WyckoffMarkup] = "wyckoff",
            [CriterionType.MaStack] = "trend",
        };

    public static AdaptiveScoringProfile Default { get; } = new(CreateDefaultComponents());

    private readonly IReadOnlyDictionary<string, AdaptiveCriterionState> _components;

    public AdaptiveScoringProfile(IReadOnlyDictionary<string, AdaptiveCriterionState> components) =>
        _components = components;

    public AdaptiveCriterionState GetState(string componentId, int baseMaxPoints) =>
        _components.TryGetValue(componentId, out var state)
            ? state
            : new AdaptiveCriterionState(1m, 50m, 0, true, baseMaxPoints);

    public static AdaptiveScoringProfile FromWeightDetails(IReadOnlyList<CriterionWeight> weights)
    {
        var map = CreateDefaultComponents();
        foreach (var w in weights)
        {
            if (!CriterionToComponent.TryGetValue(w.Type, out var id))
                continue;
            if (!map.TryGetValue(id, out var current))
                continue;

            var reliability = w.Reliability7d > 0 ? w.Reliability7d : w.Accuracy7d;
            map[id] = current with
            {
                WeightMultiplier = w.IsActive ? w.Weight : 0.25m,
                ReliabilityPercent = reliability > 0 ? reliability : 50m,
                SampleCount = w.SampleCount7d,
                IsActive = w.IsActive,
            };
        }

        return new AdaptiveScoringProfile(map);
    }

    public AdaptiveScoringProfile ScaleMultipliers(decimal factor)
    {
        var scaled = _components.ToDictionary(
            kv => kv.Key,
            kv => kv.Value with
            {
                WeightMultiplier = Math.Clamp(kv.Value.WeightMultiplier * factor, 0.25m, 2.5m),
            },
            StringComparer.OrdinalIgnoreCase);
        return new AdaptiveScoringProfile(scaled);
    }

    private static Dictionary<string, AdaptiveCriterionState> CreateDefaultComponents() =>
        BaseMaxPoints.ToDictionary(
            kv => kv.Key,
            kv => new AdaptiveCriterionState(1m, 50m, 0, true, kv.Value),
            StringComparer.OrdinalIgnoreCase);
}
