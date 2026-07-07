namespace StockRadar.Application.Options;

/// <summary>Hàm mục tiêu cho HPO (Optuna) — Phase 0/1.</summary>
public sealed class TuneEvaluateOptions
{
    public const string SectionName = "TuneEvaluate";

    public int DefaultDays { get; set; } = 60;

    /// <summary>Phiên giữ tương ứng T+2.5 (mặc định 3).</summary>
    public int HoldSessions { get; set; } = 3;

    public decimal HitRateWeight { get; set; } = 100m;

    public decimal AvgMfeWeight { get; set; } = 500m;

    public decimal MaxDrawdownWeight { get; set; } = 300m;

    public int MinTradesRequired { get; set; } = 15;

    public decimal LowTradePenaltyPerTrade { get; set; } = 10m;

    /// <summary>flexible = trừ điểm theo tỷ lệ chờ kích hoạt; strict = phase sau.</summary>
    public string AwaitingTriggerPenaltyMode { get; set; } = "flexible";
}
