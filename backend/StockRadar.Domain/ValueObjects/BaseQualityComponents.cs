namespace StockRadar.Domain.ValueObjects;

/// <summary>Thành phần điểm Base Quality Engine (sau khi qua pipeline gate).</summary>
public sealed record BaseQualityComponents(
    int PriorTrendScore,
    int AtrContractionScore,
    int CompressionScore,
    int VolumeDryScore,
    int ContractionPatternScore,
    int DistributionScore,
    int DurationScore)
{
    public int TotalScore => (int)Math.Round(
        PriorTrendScore * 0.15m
        + AtrContractionScore * 0.20m
        + CompressionScore * 0.20m
        + VolumeDryScore * 0.20m
        + ContractionPatternScore * 0.15m
        + DistributionScore * 0.05m
        + DurationScore * 0.05m);
}
