namespace StockRadar.Application.Options;

public sealed class ShadowAnalysisOptions
{
    public const string SectionName = "ShadowAnalysis";

    public bool Enabled { get; set; } = true;

    /// <summary>Ngưỡng Buy Score song song (không đổi Top hiển thị).</summary>
    public int[] VariantMinScores { get; set; } = [58, 60, 62];

  public decimal[] VariantWeightMultipliers { get; set; } = [0.9m, 1.0m, 1.1m];

    /// <summary>Số setup đo T+2.5 tối thiểu trước khi gợi ý variant tốt nhất.</summary>
    public int PromoteAfterMeasuredCount { get; set; } = 20;
}
