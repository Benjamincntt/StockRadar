namespace StockRadar.Domain.Enums;

/// <summary>
/// Tiêu chí SmartMoney (đang chấm) + chỉ báo kỹ thuật đã gỡ khỏi dây chấm điểm 09/2026.
/// Giá trị kỹ thuật GIỮ LẠI vì DB lịch sử lưu tên enum dạng string — xóa sẽ vỡ parsing dữ liệu cũ.
/// </summary>
public enum CriterionType
{
    [Obsolete("Đã gỡ khỏi dây chấm điểm 09/2026 — chỉ còn để đọc dữ liệu lịch sử")]
    Rsi = 1,
    [Obsolete("Đã gỡ khỏi dây chấm điểm 09/2026 — chỉ còn để đọc dữ liệu lịch sử")]
    MovingAverage,
    [Obsolete("Đã gỡ khỏi dây chấm điểm 09/2026 — chỉ còn để đọc dữ liệu lịch sử")]
    Macd,
    [Obsolete("Đã gỡ khỏi dây chấm điểm 09/2026 — chỉ còn để đọc dữ liệu lịch sử")]
    Volume,
    [Obsolete("Đã gỡ khỏi dây chấm điểm 09/2026 — chỉ còn để đọc dữ liệu lịch sử")]
    Vwap,
    [Obsolete("Đã gỡ khỏi dây chấm điểm 09/2026 — chỉ còn để đọc dữ liệu lịch sử")]
    BollingerBands,
    [Obsolete("Đã gỡ khỏi dây chấm điểm 09/2026 — chỉ còn để đọc dữ liệu lịch sử")]
    Atr,
    [Obsolete("Đã gỡ khỏi dây chấm điểm 09/2026 — chỉ còn để đọc dữ liệu lịch sử")]
    Ichimoku,
    [Obsolete("Đã gỡ khỏi dây chấm điểm 09/2026 — chỉ còn để đọc dữ liệu lịch sử")]
    Stochastic,
    [Obsolete("Đã gỡ khỏi dây chấm điểm 09/2026 — chỉ còn để đọc dữ liệu lịch sử")]
    Adx,

    [Obsolete("Bundle đơn giản bị loại bỏ — dùng các bundle chuyên biệt theo playbook")]
    BundleBeginner,
    [Obsolete("Bundle đơn giản bị loại bỏ — dùng các bundle chuyên biệt theo playbook")]
    BundleIntermediate,
    [Obsolete("Bundle đơn giản bị loại bỏ — dùng các bundle chuyên biệt theo playbook")]
    BundleAdvanced,
    [Obsolete("Đã gỡ khỏi dây chấm điểm 09/2026 — chỉ còn để đọc dữ liệu lịch sử")]
    BundleProfessional,
    [Obsolete("Đã gỡ khỏi dây chấm điểm 09/2026 — chỉ còn để đọc dữ liệu lịch sử")]
    BundleInstitutional,
    [Obsolete("Đã gỡ khỏi dây chấm điểm 09/2026 — chỉ còn để đọc dữ liệu lịch sử")]
    BundleSmartMoneyConcept,

    MarketPhase,
    SectorStrength,
    RelativeStrength5d,
    BaseSetup,
    BreakoutVolume,
    ShakeoutRecovery,
    VolumeSpike,
    WyckoffMarkup,
    MaStack,
}

public enum PatternBias
{
    Neutral,
    Bullish,
    Bearish,
}
