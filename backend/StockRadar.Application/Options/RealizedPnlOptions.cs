namespace StockRadar.Application.Options;

/// <summary>Phí/thuế + ngưỡng phân loại lãi lỗ, dùng cho nhật ký giao dịch (spec 010). Tách khỏi MasterAlertOptions.</summary>
public sealed class RealizedPnlOptions
{
    public const string SectionName = "RealizedPnl";

    public bool Enabled { get; set; } = true;

    /// <summary>% phí mua, cộng vào giá vốn.</summary>
    public decimal BuyFeePercent { get; set; } = 0.15m;

    /// <summary>% phí bán, trừ vào tiền thu về.</summary>
    public decimal SellFeePercent { get; set; } = 0.25m;

    /// <summary>% thuế bán, trừ vào tiền thu về.</summary>
    public decimal SellTaxPercent { get; set; } = 0.1m;

    /// <summary>Ngưỡng Win tính trên ReturnOnDeployedPercent (%). 0 = hoà vốn tính là Flat.</summary>
    public decimal WinThresholdPercent { get; set; } = 0m;

    /// <summary>Số phiên nhìn lại tối đa khi quét vị thế đóng để đo realized.</summary>
    public int MeasureLookbackSessions { get; set; } = 500;

    /// <summary>Gộp lệnh backfill giá gần đúng (T+2.5) vào số liệu tổng hợp hiển thị UI.</summary>
    public bool IncludeApproximateInAggregates { get; set; } = true;
}
