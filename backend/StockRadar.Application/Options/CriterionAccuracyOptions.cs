namespace StockRadar.Application.Options;

using StockRadar.Domain.ValueObjects;

/// <summary>Cách đo độ tin cậy chỉ báo cho trader xu hướng.</summary>
public sealed class CriterionAccuracyOptions
{
    public const string SectionName = "CriterionAccuracy";

    public int ForwardSessions { get; set; } = 5;

    /// <summary>Số ngày gom rolling accuracy (mặc định 7; tạm 3 khi mới có ít snapshot).</summary>
    public int RollingDays { get; set; } = 7;

    public int MinScoreForEvaluation { get; set; } = 60;

    public decimal DirectionThresholdPercent { get; set; } = 3m;

    public decimal SwingTargetPercent { get; set; } = 3m;

    public bool RequireTrendSetup { get; set; } = true;

    public bool RequireRelativeStrength { get; set; } = true;

    public bool RequireBaseIntact { get; set; } = true;

    /// <summary>Các khung đo bổ sung ngoài ForwardSessions (vd 10, 20 phiên).</summary>
    public int[] ExtraHorizons { get; set; } = [10, 20];

    /// <summary>Trọng số công thức reliability — chỉnh qua config sau khi backtest.</summary>
    public decimal ReliabilityHitWeight { get; set; } = 0.4m;

    public decimal ReliabilityEdgeWeight { get; set; } = 0.3m;

    public decimal ReliabilityMfeWeight { get; set; } = 0.2m;

    public decimal ReliabilityBaseIntactWeight { get; set; } = 0.1m;

    public CriterionAccuracySettings ToSettings() => new(
        ForwardSessions,
        MinScoreForEvaluation,
        DirectionThresholdPercent,
        SwingTargetPercent,
        RequireTrendSetup,
        RequireRelativeStrength,
        RequireBaseIntact,
        ReliabilityHitWeight,
        ReliabilityEdgeWeight,
        ReliabilityMfeWeight,
        ReliabilityBaseIntactWeight);
}
