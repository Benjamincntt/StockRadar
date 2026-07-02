namespace StockRadar.Infrastructure.Persistence.Entities;

public sealed class CriterionWeightEntity
{
    public string CriterionId { get; set; } = "";
    public string GroupId { get; set; } = "";
    public int Rank { get; set; }
    public decimal Weight { get; set; } = 1m;
    public decimal Accuracy7d { get; set; }
    public decimal Accuracy30d { get; set; }
    public int SampleCount7d { get; set; }
    public decimal Reliability7d { get; set; }
    public decimal Edge7d { get; set; }
    public bool IsActive { get; set; } = true;
    public string RecommendedAction { get; set; } = "Keep";
    public DateTime UpdatedAt { get; set; }
}

public sealed class DailyCriterionAccuracyEntity
{
    public DateOnly AsOfDate { get; set; }
    /// <summary>Số phiên đo forward outcome (5 = T+5, 10 = T+10, 20 = T+20).</summary>
    public int Horizon { get; set; } = 5;
    public string CriterionId { get; set; } = "";
    public string GroupId { get; set; } = "";
    public int Rank { get; set; }
    public int HitCount { get; set; }
    public int TotalCount { get; set; }
    public decimal AccuracyPercent { get; set; }
    public decimal AvgScore { get; set; }
    public decimal AvgMfePercent { get; set; }
    public decimal AvgMaePercent { get; set; }
    public decimal InvalidationRatePercent { get; set; }
    public decimal BaselinePercent { get; set; }
    public decimal EdgePercent { get; set; }
    public decimal ReliabilityScore { get; set; }
    public string BreakdownJson { get; set; } = "{}";
    public DateTime GeneratedAt { get; set; }
}

public sealed class CriterionGroupDailyAccuracyEntity
{
    public DateOnly AsOfDate { get; set; }
    public int Horizon { get; set; } = 5;
    public string GroupId { get; set; } = "";
    public int HitCount { get; set; }
    public int TotalCount { get; set; }
    public decimal AccuracyPercent { get; set; }
    public decimal AvgScore { get; set; }
    public int CriterionCount { get; set; }
    public decimal ReliabilityScore { get; set; }
    public decimal EdgePercent { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public sealed class StockCriterionScoreEntity
{
    public DateOnly AsOfDate { get; set; }
    public string Symbol { get; set; } = "";
    public int CompositeScore { get; set; }
    public decimal NextDayChangePercent { get; set; }
    public string ScoresJson { get; set; } = "[]";
    public DateTime GeneratedAt { get; set; }
}

public sealed class StockCriterionDetailEntity
{
    public DateOnly AsOfDate { get; set; }
    public int Horizon { get; set; } = 5;
    public string Symbol { get; set; } = "";
    public string CriterionId { get; set; } = "";
    public string GroupId { get; set; } = "";
    public int Rank { get; set; }
    public int Score { get; set; }
    public string Bias { get; set; } = "";
    public string Summary { get; set; } = "";
    public decimal NextDayChangePercent { get; set; }
    public bool MatchedOutcome { get; set; }
    public decimal MaxFavorablePercent { get; set; }
    public decimal MaxAdversePercent { get; set; }
    public bool InvalidatedBase { get; set; }
    public decimal RelativeStrengthForward { get; set; }
    public string ScoreBucket { get; set; } = "";
    public string MarketPhase { get; set; } = "Neutral";
    public DateTime GeneratedAt { get; set; }
}

public sealed class WeeklyCriterionReviewEntity
{
    public DateOnly WeekStartDate { get; set; }
    public string CriterionId { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string Label { get; set; } = "";
    public int Rank { get; set; }
    public int HitCount7d { get; set; }
    public int TotalCount7d { get; set; }
    public decimal Accuracy7d { get; set; }
    public decimal AvgScore7d { get; set; }
    public decimal Weight { get; set; }
    public decimal Edge7d { get; set; }
    public decimal Reliability7d { get; set; }
    public decimal AvgMfe7d { get; set; }
    public decimal InvalidationRate7d { get; set; }
    public string BreakdownJson { get; set; } = "{}";
    public string RecommendedAction { get; set; } = "Keep";
    public bool IsActive { get; set; } = true;
    public DateTime GeneratedAt { get; set; }
}

public sealed class CriterionGroupWeeklyReviewEntity
{
    public DateOnly WeekStartDate { get; set; }
    public string GroupId { get; set; } = "";
    public int HitCount { get; set; }
    public int TotalCount { get; set; }
    public decimal AccuracyPercent { get; set; }
    public decimal AvgScore { get; set; }
    public int KeepCount { get; set; }
    public int WatchCount { get; set; }
    public int RemoveCount { get; set; }
    public string RecommendedAction { get; set; } = "Keep";
    public DateTime GeneratedAt { get; set; }
}
