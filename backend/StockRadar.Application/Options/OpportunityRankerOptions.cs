namespace StockRadar.Application.Options;

public sealed class OpportunityRankerOptions
{
    public const string SectionName = "OpportunityRanker";

    public bool Enabled { get; set; } = true;

    /// <summary>Đường dẫn model JSON (tương đối ContentRoot).</summary>
    public string ModelPath { get; set; } = "Data/opportunity-ranker-model.json";

    /// <summary>MAE tối đa (âm) khi label từ MFE/MAE — ví dụ -3 = không lỗ quá 3%.</summary>
    public decimal MaxAdverseExcursionPercent { get; set; } = -3m;

    /// <summary>Khi chưa train đủ mẫu → dùng PredictedHitPercent heuristic.</summary>
    public bool FallbackToLegacyHit { get; set; } = true;

    public int DefaultDatasetDays { get; set; } = 180;

    public int TrainingEpochs { get; set; } = 800;
}
