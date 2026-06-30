namespace StockRadar.Application.Options;

using StockRadar.Domain.ValueObjects;

/// <summary>Cách đo độ tin cậy chỉ báo cho trader xu hướng.</summary>
public sealed class CriterionAccuracyOptions
{
    public const string SectionName = "CriterionAccuracy";

    public int ForwardSessions { get; set; } = 5;

    public int MinScoreForEvaluation { get; set; } = 60;

    public decimal DirectionThresholdPercent { get; set; } = 3m;

    public decimal SwingTargetPercent { get; set; } = 3m;

    public bool RequireTrendSetup { get; set; } = true;

    public bool RequireRelativeStrength { get; set; } = true;

    public bool RequireBaseIntact { get; set; } = true;

    public CriterionAccuracySettings ToSettings() => new(
        ForwardSessions,
        MinScoreForEvaluation,
        DirectionThresholdPercent,
        SwingTargetPercent,
        RequireTrendSetup,
        RequireRelativeStrength,
        RequireBaseIntact);
}
