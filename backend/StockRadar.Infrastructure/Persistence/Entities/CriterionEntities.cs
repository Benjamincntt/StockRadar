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
    public bool IsActive { get; set; } = true;
    public string RecommendedAction { get; set; } = "Keep";
    public DateTime UpdatedAt { get; set; }
}

public sealed class DailyCriterionAccuracyEntity
{
    public DateOnly AsOfDate { get; set; }
    public string CriterionId { get; set; } = "";
    public string GroupId { get; set; } = "";
    public int Rank { get; set; }
    public int HitCount { get; set; }
    public int TotalCount { get; set; }
    public decimal AccuracyPercent { get; set; }
    public decimal AvgScore { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public sealed class CriterionGroupDailyAccuracyEntity
{
    public DateOnly AsOfDate { get; set; }
    public string GroupId { get; set; } = "";
    public int HitCount { get; set; }
    public int TotalCount { get; set; }
    public decimal AccuracyPercent { get; set; }
    public decimal AvgScore { get; set; }
    public int CriterionCount { get; set; }
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
    public string Symbol { get; set; } = "";
    public string CriterionId { get; set; } = "";
    public string GroupId { get; set; } = "";
    public int Rank { get; set; }
    public int Score { get; set; }
    public string Bias { get; set; } = "";
    public string Summary { get; set; } = "";
    public decimal NextDayChangePercent { get; set; }
    public bool MatchedOutcome { get; set; }
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
