namespace StockRadar.Application.Options;

using StockRadar.Domain.ValueObjects;

/// <summary>Cấu hình outcome riêng cho từng playbook (horizon, ngưỡng hit).</summary>
public sealed class PlaybookOutcomeConfig
{
    public int ForwardSessions { get; set; } = 2;
    public decimal SwingTargetPercent { get; set; } = 3m;
    public decimal DirectionThresholdPercent { get; set; } = 3m;
}

/// <summary>Cách đo độ tin cậy chỉ báo cho trader xu hướng.</summary>
public sealed class CriterionAccuracyOptions
{
    public const string SectionName = "CriterionAccuracy";

    public int ForwardSessions { get; set; } = 2;

    /// <summary>Phiên chờ sau asOf trước khi đo (T+2.5 cần đủ T+3). Khớp OpportunityPerformance.</summary>
    public int MinSessionsBeforeMeasure { get; set; } = 3;

    /// <summary>Số ngày gom rolling accuracy (mặc định 7; tạm 3 khi mới có ít snapshot).</summary>
    public int RollingDays { get; set; } = 7;

    public int MinScoreForEvaluation { get; set; } = 60;

    public decimal DirectionThresholdPercent { get; set; } = 3m;

    public decimal SwingTargetPercent { get; set; } = 3m;

    public bool RequireTrendSetup { get; set; } = true;

    /// <summary>Bật chiều PlaybookId khi ghi accuracy. Tắt → ghi 'unclassified'. Cờ rollback.</summary>
    public bool PlaybookDimensionEnabled { get; set; } = false;

    /// <summary>
    /// Cấu hình outcome riêng cho từng playbook (horizon + ngưỡng hit).
    /// Key = string id playbook (breakout-darvas, pullback-ma20, reversal-bounce).
    /// Playbook không có entry → dùng ForwardSessions + DirectionThresholdPercent + SwingTargetPercent toàn cục.
    /// </summary>
    public Dictionary<string, PlaybookOutcomeConfig> PlaybookOutcomes { get; set; } = new()
    {
        ["breakout-darvas"]  = new PlaybookOutcomeConfig { ForwardSessions = 2, SwingTargetPercent = 3m, DirectionThresholdPercent = 3m },
        ["pullback-ma20"]    = new PlaybookOutcomeConfig { ForwardSessions = 5, SwingTargetPercent = 4m, DirectionThresholdPercent = 3m },
        ["reversal-bounce"]  = new PlaybookOutcomeConfig { ForwardSessions = 3, SwingTargetPercent = 3m, DirectionThresholdPercent = 3m },
    };

    public PlaybookOutcomeConfig GetPlaybookConfig(string playbookId) =>
        PlaybookOutcomes.GetValueOrDefault(playbookId)
        ?? new PlaybookOutcomeConfig { ForwardSessions = ForwardSessions, SwingTargetPercent = SwingTargetPercent, DirectionThresholdPercent = DirectionThresholdPercent };

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
