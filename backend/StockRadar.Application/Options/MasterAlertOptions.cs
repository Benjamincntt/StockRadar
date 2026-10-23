namespace StockRadar.Application.Options;

// BUSINESS-RULE: cổng bull-trap VNINDEX dưới đây nuôi màn Home (phần lọc Top V1); bỏ là Home mất trạng thái gate.
/// <summary>Cấu hình cổng bull-trap VNINDEX — đỉnh kháng cự + deferral chiều + hysteresis, hiển thị trên Home.</summary>
public sealed class MasterAlertOptions
{
    public const string SectionName = "MasterAlerts";

    /// <summary>
    /// Gate bull trap: sát đỉnh kháng cự VNINDEX + pha ≠ Favorable → chặn BuyPoint.
    /// Favorable vẫn cho Buy (phá đỉnh có sức).
    /// </summary>
    public bool BullTrapGateEnabled { get; set; } = true;

    /// <summary>Lookback (phiên) quét swing high VNINDEX — mặc định ~1.5 năm giao dịch.</summary>
    public int BullTrapPeakLookbackSessions { get; set; } = 400;

    /// <summary>Bán kính pivot local-max cho swing high.</summary>
    public int BullTrapPivotRadius { get; set; } = 2;

    /// <summary>% kéo từ đỉnh xuống đáy sau tối thiểu để đỉnh có nghĩa (prominence).</summary>
    public decimal BullTrapMinProminencePercent { get; set; } = 5m;

    /// <summary>Index cách đỉnh kháng cự ≤ band (%) thì coi là NearPriorPeak.</summary>
    public decimal BullTrapNearPeakBandPercent { get; set; } = 1.5m;

    // --- Slice 1: bull-trap deferral (chờ chiều xác nhận trước khi bắn Buy) ---

    /// <summary>
    /// Trong trap-zone (env bật hoặc đã xuyên đỉnh đã ghim), hoãn Buy1/Buy2-breakout tới
    /// checkpoint chiều rồi mới bắn nếu shape còn giữ. Chống bẫy xanh-sáng-xả-chiều.
    /// </summary>
    public bool BullTrapDeferralEnabled { get; set; } = true;

    /// <summary>Giờ checkpoint chiều (mặc định 14:00 — sau con sóng xả 13:00→14:00, không phải mép 13:00).</summary>
    public int BullTrapDeferralCheckpointHour { get; set; } = 14;

    /// <summary>Phút checkpoint chiều.</summary>
    public int BullTrapDeferralCheckpointMinute { get; set; }

    /// <summary>Tại checkpoint, mã phải còn sát high phiên: (High−Close)/High ≤ % này.</summary>
    public decimal BullTrapDeferralCloseWithinHighPercent { get; set; } = 1.5m;

    /// <summary>
    /// Yêu cầu shape giữ <b>suốt</b> cửa sổ 13:00→checkpoint (không chỉ đọc một lần tại checkpoint).
    /// </summary>
    public bool BullTrapDeferralRequireWindowIntegrity { get; set; }

    /// <summary>
    /// Yêu cầu khối ngoại chưa quay đầu bán từ đầu chiều (13:00) mới cho bắn qua checkpoint.
    /// Thiếu dữ liệu foreign → fail-open.
    /// </summary>
    public bool BullTrapDeferralRequireForeignHold { get; set; }

    // --- Slice 2: hysteresis quanh mép near-peak (chống nhiễu, độc lập với pin) ---

    /// <summary>Bật hysteresis cho <c>IsNearPriorPeak</c> — chống flicker env quanh mép band.</summary>
    public bool BullTrapHysteresisEnabled { get; set; } = true;

    /// <summary>Chỉ tắt "Gần đỉnh" khi live lùi xa hơn % này (≥ <see cref="BullTrapNearPeakBandPercent"/>).</summary>
    public decimal BullTrapNearPeakExitBandPercent { get; set; } = 2m;

    /// <summary>
    /// Pha Neutral cũng kích hoạt bull-trap env (cứng). Mặc định <c>false</c>: chỉ Unfavorable mới bị
    /// chặn — Neutral là pha trung tính, cơ hội tốt hơn thị trường vẫn hợp lệ nhưng bị defer chiều.
    /// </summary>
    public bool BullTrapBlockOnNeutral { get; set; } = false;

    /// <summary>
    /// Trong bull-trap env, breakout/pullback không bị hardblock mà được đẩy vào deferral checkpoint chiều.
    /// Mặc định <c>true</c>: chỉ Unfavorable sát đỉnh mới bị defer thay vì bỏ hẳn.
    /// </summary>
    public bool BullTrapSoftBlockEnabled { get; set; } = true;

    /// <summary>Số phiên prior quét nến đỏ (rũ) cho Buy1 dip-bounce trong bull-trap env.</summary>
    public int BullTrapDipLookbackSessions { get; set; } = 3;

    /// <summary>Số phiên giảm tối thiểu trong lookback để coi là đã rũ.</summary>
    public int BullTrapMinRedSessions { get; set; } = 2;

    /// <summary>
    /// Bull-trap env: Buy2 scale-in khi lãi so giá Buy1 ≥ ngưỡng này (%). Không yêu cầu vol/ticks/ML.
    /// </summary>
    public decimal BullTrapBuy2ScaleInGainPercent { get; set; } = 10m;
}
