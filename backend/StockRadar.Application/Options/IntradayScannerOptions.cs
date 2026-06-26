namespace StockRadar.Application.Options;

public sealed class IntradayScannerOptions
{
    public const string SectionName = "IntradayScanner";

    public bool Enabled { get; set; } = true;

    /// <summary>Chu kỳ quét trong phiên (giây).</summary>
    public int IntervalSeconds { get; set; } = 120;

    /// <summary>Sàn lọc: HOSE (mặc định).</summary>
    public string Exchange { get; set; } = "HOSE";

    /// <summary>KL tích lũy phiên tối thiểu.</summary>
    public long MinSessionVolume { get; set; } = 1_000_000;

    /// <summary>|% thay đổi| tối thiểu so tham chiếu.</summary>
    public decimal MinAbsChangePercent { get; set; } = 3m;

    public int BatchSize { get; set; } = 40;

    /// <summary>Quét cả ngoài giờ (dev/test).</summary>
    public bool ForceScanOutsideHours { get; set; }
}
