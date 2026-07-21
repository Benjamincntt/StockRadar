namespace StockRadar.Domain.Services.ReversalBounce;

/// <summary>Stage của một "đợt tìm đáy" suy ra từ OHLCV (stateless). Dùng chung cho snapshot và signal.</summary>
public enum ReversalBounceStage
{
    /// <summary>Chưa từng có đợt bán tháo trong lookback → không phải ứng viên.</summary>
    None = 0,

    /// <summary>Capitulating — đang bán tháo, chưa cân bằng.</summary>
    Capitulating = 1,

    /// <summary>Stabilizing — ngừng rơi, lực bán suy yếu.</summary>
    Stabilizing = 2,

    /// <summary>Confirmed — xuất hiện cầu mua xác nhận.</summary>
    Confirmed = 3,

    /// <summary>Invalidated — mất hiệu lực (thủng đáy / mất vùng xác nhận / regime xấu).</summary>
    Invalidated = 4,
}
