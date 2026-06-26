using StockRadar.Domain.ValueObjects;

namespace StockRadar.Application.Options;

public sealed class PriceRunupFilterOptions
{
    public const string SectionName = "PriceRunupFilter";

    /// <summary>Biên độ tối đa (high-low)/low trong vùng nền (%).</summary>
    public decimal ConsolidationMaxRangePercent { get; set; } = 15m;

    /// <summary>Tối thiểu số phiên để coi là một vùng nền.</summary>
    public int ConsolidationMinSessions { get; set; } = 5;

    /// <summary>Số phiên lùi tối đa khi tìm vùng nền.</summary>
    public int MaxScanSessions { get; set; } = 120;

    /// <summary>Loại mã đã tăng quá ngưỡng này so với đỉnh nền (%).</summary>
    public decimal MaxGainFromBasePercent { get; set; } = 10m;

    /// <summary>Giá đóng cuối kỳ lệch tối đa so với đầu kỳ trong một vùng nền (%).</summary>
    public decimal MaxCloseDriftPercent { get; set; } = 8m;

    /// <summary>Hai đoạn tích lũy chỉ gộp cùng một nền khi mức giá tương đồng (midpoint lệch ≤ ngưỡng).</summary>
    public decimal MaxBandSeparationPercent { get; set; } = 5m;

    public BasePriceFilterSettings ToSettings() => new(
        ConsolidationMaxRangePercent,
        ConsolidationMinSessions,
        MaxScanSessions,
        MaxGainFromBasePercent,
        MaxCloseDriftPercent,
        MaxBandSeparationPercent);
}
