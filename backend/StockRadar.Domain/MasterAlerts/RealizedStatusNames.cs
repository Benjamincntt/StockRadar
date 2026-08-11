namespace StockRadar.Domain.MasterAlerts;

/// <summary>
/// Giá trị <c>MasterAlertPositionEntity.RealizedStatus</c> — trạng thái đo realized P&amp;L của 1 vị thế đã đóng.
/// </summary>
public static class RealizedStatusNames
{
    /// <summary>Giá bán thật từ tín hiệu VIP (<c>VipAlertFires</c>).</summary>
    public const string Measured = "Measured";

    /// <summary>Ít nhất 1 leg là giá backfill (T+2.5/OHLCV close), không phải giá bắn noti thật.</summary>
    public const string Approximate = "Approximate";

    /// <summary>Không dựng được leg nào (hoặc giá vào lệnh không hợp lệ) — không quét lại vô hạn.</summary>
    public const string MissingSellPrice = "MissingSellPrice";
}
