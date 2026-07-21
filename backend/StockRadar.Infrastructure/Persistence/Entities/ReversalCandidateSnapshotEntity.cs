namespace StockRadar.Infrastructure.Persistence.Entities;

/// <summary>
/// Snapshot ứng viên counter-trend "sóng hồi" — bất biến theo
/// unique <c>(TradingDate, Symbol, StrategyVersion, SetupId)</c>.
/// </summary>
public sealed class ReversalCandidateSnapshotEntity
{
    public Guid Id { get; set; }
    public DateOnly TradingDate { get; set; }
    public string Symbol { get; set; } = "";
    public string Stage { get; set; } = "";
    public Guid SetupId { get; set; }

    public DateOnly? CapitulationDate { get; set; }
    public decimal? CapitulationLow { get; set; }
    public decimal? CapitulationClose { get; set; }
    public int RecoveryAttemptCount { get; set; }

    public decimal ScoreCapitulation { get; set; }
    public decimal ScoreStabilization { get; set; }
    public decimal ScoreDemand { get; set; }
    public decimal ScoreRelativeStrength { get; set; }
    public decimal ScoreLiquidity { get; set; }
    public decimal ScoreRiskPenalty { get; set; }
    public decimal TotalScore { get; set; }

    public string MarketRegime { get; set; } = "Normal";
    public bool IsActionable { get; set; }

    // Trade plan (null nếu chưa Confirmed / fail hard gate).
    public decimal? EntryReference { get; set; }
    public decimal? MaxEntryPrice { get; set; }
    public decimal? InvalidationPrice { get; set; }
    public decimal? FirstTarget { get; set; }
    public decimal? RewardToRisk { get; set; }
    public decimal? PositionFactor { get; set; }
    public int? TimeStopSessions { get; set; }
    public string RiskWarningsJson { get; set; } = "[]";

    public string StrategyVersion { get; set; } = "";
    public string AlgorithmParametersHash { get; set; } = "";
    public int SchemaVersion { get; set; }
    public Guid RunBatchId { get; set; }
    public string ReasonsJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; }
}
